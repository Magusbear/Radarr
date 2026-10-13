using System;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;

namespace NzbDrone.Core.MediaFiles.BurnedInSubtitles
{
    public interface IBurnedInSubtitleDetector
    {
        BurnedInSubtitleResult DetectChinese(string path, TimeSpan runtime);
    }

    public enum BurnedInSubtitleStatus
    {
        Unknown,
        NotDetected,
        Detected
    }

    public class BurnedInSubtitleResult
    {
        public BurnedInSubtitleStatus Status { get; set; }
        public int SampledFrames { get; set; }
        public int FramesWithText { get; set; }
    }

    // Samples frames spread over the movie (skipping intro and credits) and counts the frames whose subtitle
    // area contains Chinese text. Burned-in subtitles show up in many frames, on-screen text only in a few.
    public class BurnedInSubtitleDetector : IBurnedInSubtitleDetector
    {
        public const int SampleCount = 12;
        public const int MinimumFramesWithText = 3;
        public const int MinimumCharactersPerFrame = 3;
        public const int MinimumConfidence = 60;

        private static readonly Regex ChineseCharacterRegex = new(@"[㐀-䶿一-鿿豈-﫿]", RegexOptions.Compiled);

        private readonly ISubtitleFrameReader _frameReader;
        private readonly Logger _logger;

        public BurnedInSubtitleDetector(ISubtitleFrameReader frameReader, Logger logger)
        {
            _frameReader = frameReader;
            _logger = logger;
        }

        public BurnedInSubtitleResult DetectChinese(string path, TimeSpan runtime)
        {
            var result = new BurnedInSubtitleResult { Status = BurnedInSubtitleStatus.Unknown };

            if (!_frameReader.IsAvailable() || runtime <= TimeSpan.Zero)
            {
                return result;
            }

            for (var i = 0; i < SampleCount; i++)
            {
                // Spread samples between 10% and 90% of the runtime
                var position = TimeSpan.FromSeconds(runtime.TotalSeconds * (0.1 + (0.8 * (i + 0.5) / SampleCount)));
                var ocrOutput = _frameReader.ReadSubtitleArea(path, position);

                if (ocrOutput == null)
                {
                    continue;
                }

                result.SampledFrames++;

                var characters = CountChineseCharacters(ocrOutput);

                if (characters >= MinimumCharactersPerFrame)
                {
                    result.FramesWithText++;
                    _logger.Trace("Chinese text in subtitle area at {0:hh\\:mm\\:ss}: {1} characters", position, characters);
                }
            }

            if (result.SampledFrames < SampleCount / 2)
            {
                _logger.Debug("Only {0} of {1} frames could be read from {2}", result.SampledFrames, SampleCount, path);
                return result;
            }

            result.Status = result.FramesWithText >= MinimumFramesWithText ? BurnedInSubtitleStatus.Detected : BurnedInSubtitleStatus.NotDetected;

            return result;
        }

        // Counts Chinese characters in words tesseract is confident about (TSV columns: ... conf text)
        public static int CountChineseCharacters(string tesseractTsv)
        {
            return tesseractTsv.Split('\n')
                .Skip(1)
                .Select(line => line.Split('\t'))
                .Where(columns => columns.Length >= 12 &&
                                  double.TryParse(columns[10], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var confidence) &&
                                  confidence >= MinimumConfidence)
                .Sum(columns => ChineseCharacterRegex.Matches(columns[11]).Count);
        }
    }
}
