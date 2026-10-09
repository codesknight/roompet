using UnityEditor;
using UnityEngine;

namespace DshMobile.EditorTools
{
    /// <summary>
    /// Import settings for the background music, applied to anything dropped under
    /// <c>Resources/Music</c>.
    ///
    /// Set here rather than by hand per file for two reasons. The first is that a hand-set
    /// importer is invisible: the next track someone adds would ship decompressed-on-load and
    /// nobody would notice until the phone's memory graph did. The second is that the right
    /// settings are not the defaults in any direction — music is the one kind of audio that
    /// *should* stream, because a two-minute track decoded into memory costs tens of megabytes
    /// and is heard exactly once per loop.
    /// </summary>
    public class MusicImportPostprocessor : AssetPostprocessor
    {
        private const string Folder = "/Resources/Music/";

        /// <summary>Vorbis quality for music: 0.7 is roughly 160 kbps, which is transparent
        /// enough on a phone speaker and keeps the APK from doubling.</summary>
        private const float Quality = 0.7f;

        private void OnPreprocessAudio()
        {
            string path = assetPath.Replace('\\', '/');
            if (path.IndexOf(Folder, System.StringComparison.OrdinalIgnoreCase) < 0) return;

            var importer = assetImporter as AudioImporter;
            if (importer == null) return;

            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = Quality;
            settings.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;

            // Preloading a streamed clip is a contradiction. In this Unity version the flag lives
            // on the per-platform sample settings (the same-named property on the importer itself
            // is obsolete and throws at compile time).
            settings.preloadAudioData = false;
            importer.defaultSampleSettings = settings;

            // Mono would throw away the stereo image the tracks were written with.
            importer.loadInBackground = true;
            importer.forceToMono = false;
        }
    }
}
