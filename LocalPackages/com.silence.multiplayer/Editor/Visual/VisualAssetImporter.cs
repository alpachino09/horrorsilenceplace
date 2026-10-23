using UnityEditor;
using UnityEngine;

namespace Silence.Multiplayer.Editor.Visual
{
    public sealed class VisualAssetImporter : AssetPostprocessor
    {
        private const string Art = "Assets/_Silence/Art/VisualRebuild/";
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Art, System.StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            importer.mipmapEnabled = true; importer.anisoLevel = 8;
            importer.maxTextureSize = 1024; importer.wrapMode = TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            bool normal = assetPath.EndsWith("_Normal.png", System.StringComparison.Ordinal);
            bool data = normal || assetPath.EndsWith("_Mask.png", System.StringComparison.Ordinal) || assetPath.Contains("HeightPreview");
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !data;
            if (assetPath.Contains("Atlas") || assetPath.Contains("Needles"))
            { importer.alphaIsTransparency = true; importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.mipMapsPreserveCoverage = true;importer.alphaTestReferenceValue=.38f; }
            if (assetPath.Contains("FlashlightCookie"))
            { importer.wrapMode = TextureWrapMode.Clamp; importer.sRGBTexture = false; importer.alphaSource = TextureImporterAlphaSource.FromGrayScale; }
        }
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Art, System.StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
            importer.generateSecondaryUV = true;
            importer.importAnimation = false; importer.addCollider = false; importer.isReadable = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.meshCompression = ModelImporterMeshCompression.Low;
        }
    }
}
