package cmdlipinstall

import (
	"fmt"
	"net/url"
	"os"

	"github.com/blang/semver/v4"
	"github.com/lippkg/lip/internal/context"
	"github.com/lippkg/lip/internal/network"
	"github.com/lippkg/lip/internal/path"
	"github.com/lippkg/lip/internal/tooth"
	log "github.com/sirupsen/logrus"
)

func downloadFileIfNotCached(ctx context.Context, downloadURL *url.URL) (path.Path, error) {
	cachePath, err := getCachePath(ctx, downloadURL)
	if err != nil {
		return path.Path{}, fmt.Errorf("failed to get cache path: %w", err)
	}

	// Skip downloading if the file is already in the cache.
	if _, err := os.Stat(cachePath.LocalString()); os.IsNotExist(err) {
		log.Infof("Downloading %v...", downloadURL)

		var enableProgressBar bool
		if log.GetLevel() == log.PanicLevel || log.GetLevel() == log.FatalLevel ||
			log.GetLevel() == log.ErrorLevel || log.GetLevel() == log.WarnLevel {
			enableProgressBar = false
		} else {
			enableProgressBar = true
		}

		if err := network.DownloadFile(downloadURL, cachePath, enableProgressBar); err != nil {
			return path.Path{}, fmt.Errorf("failed to download file: %w", err)
		}

	} else if err != nil {
		return path.Path{}, fmt.Errorf("failed to check if file exists: %w", err)
	}

	return cachePath, nil
}

// downloadToothArchiveIfNotCached downloads the tooth archive from the Go module proxy
// if it is not cached, and returns the path to the downloaded tooth archive.
func downloadToothArchiveIfNotCached(ctx context.Context, toothRepoPath string,
	toothVersion semver.Version) (tooth.Archive, error) {

	goModuleProxyURL, err := ctx.GoModuleProxyURL()
	if err != nil {
		return tooth.Archive{}, fmt.Errorf("failed to get Go module proxy URL: %w", err)
	}

	downloadURL, err := network.GenerateGoModuleZipFileURL(toothRepoPath, toothVersion, goModuleProxyURL)
	if err != nil {
		return tooth.Archive{}, fmt.Errorf("failed to generate Go module zip file URL: %w", err)
	}

	cachePath, err := downloadFileIfNotCached(ctx, downloadURL)
	if err != nil {
		return tooth.Archive{}, fmt.Errorf("failed to download file: %w", err)
	}

	archive, err := tooth.MakeArchive(cachePath)
	if err != nil {
		return tooth.Archive{}, fmt.Errorf("failed to open archive %v: %w", cachePath, err)
	}

	if err := validateToothArchive(archive, toothRepoPath, toothVersion); err != nil {
		return tooth.Archive{}, fmt.Errorf("failed to validate archive: %w", err)
	}

	return archive, nil
}

func downloadToothAssetArchiveIfNotCached(ctx context.Context, archive tooth.Archive) error {
	metadata := archive.Metadata()
	assetURL, err := metadata.AssetURL()
	if err != nil {
		return fmt.Errorf("failed to get asset URL: %w", err)
	}

	if assetURL.String() == "" {
		return nil
	}

	// Rewrite GitHub URL to GitHub mirror URL if it is set.

	gitHubMirrorURL, err := ctx.GitHubMirrorURL()
	if err != nil {
		return fmt.Errorf("failed to get GitHub mirror URL: %w", err)
	}

	if network.IsGitHubURL(assetURL) && gitHubMirrorURL.String() != "" {
		mirroredURL, err := network.GenerateGitHubMirrorURL(assetURL, gitHubMirrorURL)
		if err != nil {
			return fmt.Errorf("failed to generate GitHub mirror URL: %w", err)
		}

		log.Infof("GitHub URL detected. Rewrite URL to %v", gitHubMirrorURL)

		assetURL = mirroredURL
	}

	if _, err := downloadFileIfNotCached(ctx, assetURL); err != nil {
		return fmt.Errorf("failed to download file: %w", err)
	}

	return nil
}

func getCachePath(ctx context.Context, u *url.URL) (path.Path, error) {
	cacheDir, err := ctx.CacheDir()
	if err != nil {
		return path.Path{}, fmt.Errorf("failed to get cache directory: %w", err)
	}

	cacheFileName := url.QueryEscape(u.String())
	cachePath := cacheDir.Join(path.MustParse(cacheFileName))

	return cachePath, nil
}
