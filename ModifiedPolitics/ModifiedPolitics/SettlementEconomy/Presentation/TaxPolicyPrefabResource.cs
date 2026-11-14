// Resolves tax-policy UI fragments beside ModifiedPolitics.dll, matching the
// module's existing UIExtenderEx resource convention used by KingdomFief,
// KingdomClan, HeroOffices, and settlement-population presentation resources.
using System;
using System.IO;
using System.Reflection;
using System.Xml;

namespace ModifiedPolitics.SettlementEconomy.Presentation
{
    /// <summary>Loads one tax-policy prefab fragment from its module-root path.</summary>
    internal static class TaxPolicyPrefabResource
    {
        /// <summary>
        /// Resolves a DLL-adjacent presentation resource and returns an XML
        /// element suitable for UIExtenderEx Prefabs2 insertion.
        /// </summary>
        internal static XmlNode Load(string fileName)
        {
            string assemblyDirectory = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(assemblyDirectory))
                throw new InvalidOperationException(
                    "ModifiedPolitics assembly directory is unavailable.");

            string resourcePath = Path.Combine(assemblyDirectory, "SettlementEconomy",
                "Presentation", fileName);

            var document = new XmlDocument();
            document.Load(resourcePath);
            return document.DocumentElement;
        }
    }
}
