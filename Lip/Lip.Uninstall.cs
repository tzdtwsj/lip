using Microsoft.Extensions.Logging;
using Semver;
using System.Runtime.InteropServices;

namespace Lip;

public partial class Lip
{
    public record UninstallArgs
    {
        public required bool DryRun { get; init; }
        public required bool IgnoreScripts { get; init; }
    }

    private record PackageUninstallDetail : TopoSortedPackageList<PackageUninstallDetail>.IItem
    {
        public Dictionary<PackageIdentifier, SemVersionRange> Dependencies
        {
            get
            {
                return Manifest.GetSpecifiedVariant(
                        VariantLabel,
                        RuntimeInformation.RuntimeIdentifier)?
                        .Dependencies?
                        .Select(
                            kvp => new KeyValuePair<PackageIdentifier, SemVersionRange>(
                                PackageIdentifier.Parse(kvp.Key),
                                SemVersionRange.ParseNpm(kvp.Value)))
                        .ToDictionary()
                        ?? [];
            }
        }

        public required PackageManifest Manifest { get; init; }

        public PackageSpecifier Specifier => new()
        {
            ToothPath = Manifest.ToothPath,
            VariantLabel = VariantLabel,
            Version = Manifest.Version
        };

        public required string VariantLabel { get; init; }
    }

    public async Task Uninstall(List<string> packageSpecifierTextsToUninstall, UninstallArgs args)
    {
        List<PackageIdentifier> packageSpecifiersToUninstallSpecified =
            packageSpecifierTextsToUninstall.ConvertAll(PackageIdentifier.Parse);

        // Remove non-installed packages and sort packages topologically.

        TopoSortedPackageList<PackageUninstallDetail> packageUninstallDetails = [];

        foreach (PackageIdentifier packageSpecifier in packageSpecifiersToUninstallSpecified)
        {
            PackageManifest? packageManifest = await _packageManager.GetPackageManifestFromInstalledPackages(
                packageSpecifier);

            if (packageManifest is null)
            {
                _context.Logger.LogWarning(
                    "Package '{packageSpecifier}' is not installed. Skipping.",
                    packageSpecifier);
                continue;
            }

            packageUninstallDetails.Add(new PackageUninstallDetail
            {
                Manifest = packageManifest,
                VariantLabel = packageSpecifier.VariantLabel
            });
        }

        // Uninstall packages in topological order.

        foreach (PackageUninstallDetail packageUninstallDetail in packageUninstallDetails)
        {
            await _packageManager.UninstallPackage(
                packageUninstallDetail.Specifier.Identifier,
                args.DryRun,
                args.IgnoreScripts);
        }
    }
}
