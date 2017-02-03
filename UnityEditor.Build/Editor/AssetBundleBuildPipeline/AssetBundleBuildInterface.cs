#if EXAMPLE || true

using Unity.Bindings;
using UnityEngine;

namespace UnityEditor
{
    public struct ObjectIdentifier
    {
        /// <summary>
        /// Source file on disk that contains this asset
        /// </summary>
        public GUID guid;

        /// <summary>
        /// Many files may contain multiple objects of the same type. This makes each one unique within the file.
        /// </summary>
        public long localIdentifierInFile;

        /// <summary>
        /// The type of the object in the file
        /// </summary>
        public int type;

        public override string ToString()
        {
            return UnityString.Format("{{guid: {1}, fileID: {0}, type: {2}}}", guid, localIdentifierInFile, type);
        }
    }

    /// <summary>
    /// Additions that would maintain this as the primary source of information for source assets
    /// </summary>
    /// <remarks>splitting this out would clarify the bundle building system's api considerably.</remarks>
    public partial class AssetDatabase
    {
        // Get an array of all objects that are in an asset identified by GUID
        // Similar concept to AssetDatabase.LoadAllAssets but doesn't actually load content, only gathers information.
        // Also returns localIdentifiers (why do we need this?)
        extern public static ObjectIdentifier[] GetObjectIdentifiersInAsset(GUID asset);

        // Get an array of all dependencies for an object identified by ObjectIdentifier
        // DDP: Does the returned list also include the passed ObjectIdentifier? Does not specify.
        // Notes: Due to the current asset database limitations, this api will only work for the currently active build target. We want to change this to take a built target, but will require new asset database.
        // DDP: Why would you want to build assets for a different build target in the same build run?
        extern public static ObjectIdentifier[] GetPlayerDependenciesForObject(ObjectIdentifier objectID);

        // Get an array of all dependencies for an array of objects identified by ObjectIdentifier.
        // DDP: Does the returned list also include the passed ObjectIdentifier? Does not specify.
        // Batch api to reduce C++ <> C# calls
        // Notes: Due to the current asset database limitations, this api will only work for the currently active build target. We want to change this to take a built target, but will require new asset database.
        // DDP: Why would you want to build assets for a different build target in the same build run?
        extern public static ObjectIdentifier[] GetPlayerDependenciesForObjects(ObjectIdentifier[] objectIDs);

        // Get an array of all dependencies for an asset.
        // DDP: Does the returned list also include the passed asset? Does not specify.
        // simpler API, wrapper on top of previous definitions.
        extern public static ObjectIdentifier[] GetPlayerDependenciesForAsset(GUID asset);

        // Get an array of all dependencies for an asset.
        // DDP: Does the returned list also include the passed assets? Does not specify.
        // simpler API, wrapper on top of previous definitions.
        extern public static ObjectIdentifier[] GetPlayerDependenciesForAssets(GUID[] assets);
    }
}


namespace UnityEditor.AssetBundles
{
    // DDP - I have renamed many things in the interest of API clarity.
    // There is no intent to change the underlying behavior.
    // For example, prefixing everything with "AssetBundle" is unnecessary and cluttering.
    // That's what namespaces are for.

    /// <summary>
    /// The list of bundle assignments as defined in the Editor Inspector by the "asset bundle" dropdown
    /// </summary>
    public struct EditorDefinedBundles   // was AssetBundleBuildInput
    {
        public struct Definition
        {
            // Do you plan on integrating the Variants here?
            // Or will that be added to the assetBundleName automatically?
            public string assetBundleName;
            public GUID[] explicitAssets;
        }

        public Definition[] definitions;
    }


    public enum CompressionType
    {
        None,
        Lzma,
        Lz4,
        Lz4HC,
        Lzham,
    }

    public enum CompressionLevel
    {
        None,
        Fastest,
        Fast,
        Normal,
        High,
        Maximum,
    }

    public struct BuildCompression
    {
        // Default block size compression to be used with blockSize below
        public const uint DefaultCompressionBlockSize = 131072; //128 * 1024;

        public CompressionType compression;
        public CompressionLevel level;
        public uint blockSize;
        public bool streamed;
    }

    public struct BuildSettings // AssetBundleBuildSettings
    {
        // DDP - this should be temp folder, right? Where ResourceFiles are written?
        // As a developer I would want them written to a different location than the final bundle files.
        public string outputFolder;
        public BuildTarget target;
        public bool streamingResources;
        public bool editorBundles;
    }

    /// <summary>
    /// Defines a single bundle that will be created
    /// </summary>
    public struct BuildCommand
    {
        public struct AssetLoadInfo
        {
            public GUID asset;
            public ObjectIdentifier[] includedObjects;
            public ObjectIdentifier[] referencedObjects;
        }

        /// <summary>
        /// the desired bundle file name
        /// </summary>
        public string assetBundleName;

        /// <summary>
        /// List of assets that can be directly requested by client.
        /// </summary>
        /// <remarks>Can be trimmed to minimize what's available to client</remarks>
        public AssetLoadInfo[] explicitAssets;

        /// <summary>
        /// The list of objects that are actually included in this bundle
        /// </summary>
        /// <remarks>Literally, the contents of this bundle. 
        /// All contents of explicitAssets must either be available in this list or the list
        /// of one of the bundles we depend on.</remarks>
        public ObjectIdentifier[] assetBundleObjects;

        /// <summary>
        /// List of asset bundle names that this bundle depends on.
        /// </summary>
        /// <remarks>
        /// client is expected to have these bundles open/loaded before extracting content from this one.
        /// This list plus AssetDatabase.GetAssetDependencyHash() for each source file is enough to determine
        /// if a given bundle needs regeneration.
        /// </remarks>
        public string[] assetBundleDependencies;
    }


    /// <summary>
    /// Details of a single bundle that has been created
    /// </summary>
    /// <remarks>Expect one of these for each BuildCommand</remarks>
    public struct BuildResult
    {
        public struct ResourceFile
        {
            public string fileName;
            public bool serializedFile;
        }
        
        /// <summary>
        /// Resulting filename (matches BuildCommand.assetBundleName)
        /// </summary>
        public string assetBundleName;

        // DDP - this feels like it needs an error string, so caller can know what went wrong, if anything

        /// <summary>
        /// Internal file parts to be passed to archiver.
        /// </summary>
        public ResourceFile[] resourceFiles;

        // The following is purely informational.
        public GUID[] explicitAssets;
        public ObjectIdentifier[] assetBundleObjects;
        public string[] assetBundleDependencies;
        public Hash128 targetHash;
        public Hash128 typeTreeLayoutHash;
        public System.Type[] includedTypes;
    }

    public class RawBuildInterface
    {
        // Generates an array of all asset bundles and the assets they include
        // Notes: Pre-dreprecated as we want to move asset bundle data off of asset meta files and into it's own asset
        // DDP: This worries me, as that means the asset's meta file is no longer the single source of information for all settings pertaining to this asset.
        // DDP: Unclear if this provides only bundle definitions that have changed or full list.
        // extern public static AssetBundleBuildInput GenerateAssetBundleBuildInput();
        extern public static EditorDefinedBundles GetEditorDefinedBundles();

        // The build process is in two phases:
        // 1. Create uncompressed "raw" bundle data (WriteResourcefilesForAssetBundles)
        // 2. For each raw bundle, compress and write final file to disk for distribution (ArchiveAndCompressAssetBundle)

        // Writes out SerializedFile and Resource files for each bundle defined in CommandList
        extern public static BuildResult[] WriteResourcefilesForAssetBundles(BuildCommand[] commands, BuildSettings settings);

        // Archives and compresses SerializedFile and Resource files for a single asset bundle
        // DDP - there is no error reporting here. Returning a string would be sufficient.
        // DDP - I can see how the other pieces might want to be broken out, but should we allow
        // callers to mess with the resourceFiles list? Maybe just pass a single BuildResult?
        extern public static void ArchiveAndCompressAssetBundle(BuildResult.ResourceFile[] resourceFiles, string outputBundlePath, BuildCompression compression);

        // DDP - Can we finally get an API for WWW to generate a WWW.CRC from a file on disk?

        // TODO:
        // Incremental building of asset bundles
        // Maybe find some better names for some types / fields. IE: CommandList.Command is kinda awkward
    }
}

#endif
