using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.MediaFiles.BurnedInSubtitles
{
    public interface ISubtitleFrameReader
    {
        bool IsAvailable();

        // Returns tesseract TSV output for the subtitle area of the frame at the given position, null on failure
        string ReadSubtitleArea(string path, TimeSpan position);
    }

    // Extracts the bottom of a frame with ffmpeg (where subtitles are), keeps only bright pixels (subtitle text is
    // white or yellow) as black text on white and runs tesseract with the installed Chinese models on it.
    public class SubtitleFrameReader : ISubtitleFrameReader
    {
        private const string FrameFilter = "crop=iw:ih*0.28:0:ih*0.72,scale=1920:-2,format=gray,lut=c0='if(gte(val,180),0,255)'";
        private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(60);
        private static readonly string[] ChineseModels = { "chi_sim", "chi_tra" };

        private readonly Logger _logger;
        private readonly Lazy<string> _languages;

        public SubtitleFrameReader(Logger logger)
        {
            _logger = logger;
            _languages = new Lazy<string>(FindLanguages);
        }

        public bool IsAvailable()
        {
            return _languages.Value.IsNotNullOrWhiteSpace();
        }

        public string ReadSubtitleArea(string path, TimeSpan position)
        {
            var framePath = Path.Combine(Path.GetTempPath(), $"radarr-subtitle-{Guid.NewGuid():N}.png");

            try
            {
                var ffmpegArguments = new[]
                {
                    "-nostdin", "-hide_banner", "-loglevel", "error",
                    "-ss", position.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture),
                    "-i", path,
                    "-frames:v", "1",
                    "-vf", FrameFilter,
                    "-y", framePath
                };

                var ffmpeg = Run("ffmpeg", ffmpegArguments);

                if (ffmpeg == null || !File.Exists(framePath))
                {
                    return null;
                }

                return Run("tesseract", framePath, "stdout", "-l", _languages.Value, "--psm", "6", "tsv");
            }
            finally
            {
                if (File.Exists(framePath))
                {
                    File.Delete(framePath);
                }
            }
        }

        private string FindLanguages()
        {
            var output = Run("ffmpeg", "-hide_banner", "-version") == null ? null : Run("tesseract", "--list-langs");

            if (output == null)
            {
                _logger.Warn("ffmpeg or tesseract not found, burned-in subtitle detection is not available");
                return null;
            }

            var installed = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var languages = ChineseModels.Where(installed.Contains).ToList();

            if (languages.Empty())
            {
                _logger.Warn("No Chinese tesseract language data installed (chi_sim/chi_tra), burned-in subtitle detection is not available");
                return null;
            }

            _logger.Debug("Burned-in subtitle detection uses tesseract languages: {0}", string.Join("+", languages));

            return string.Join("+", languages);
        }

        private string Run(string fileName, params string[] arguments)
        {
            var startInfo = new ProcessStartInfo(fileName)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            // tesseract uses all cores per page by default, one is plenty for a subtitle strip
            startInfo.Environment["OMP_THREAD_LIMIT"] = "1";

            try
            {
                using var process = Process.Start(startInfo);

                var standardOutput = process.StandardOutput.ReadToEndAsync();
                var standardError = process.StandardError.ReadToEndAsync();

                if (!process.WaitForExit((int)ProcessTimeout.TotalMilliseconds))
                {
                    process.Kill(true);
                    _logger.Debug("{0} timed out after {1}s", fileName, ProcessTimeout.TotalSeconds);
                    return null;
                }

                if (process.ExitCode != 0)
                {
                    _logger.Debug("{0} exited with {1}: {2}", fileName, process.ExitCode, standardError.Result.Trim());
                    return null;
                }

                return standardOutput.Result;
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                _logger.Debug(ex, "Unable to run {0}", fileName);
                return null;
            }
        }
    }
}
