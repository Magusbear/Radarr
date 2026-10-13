using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.MediaInfo;

namespace NzbDrone.Core.CustomFormats
{
    // Rewrites a release title so that Release Title custom format conditions match what
    // ffprobe found in the file instead of what the release name claims. Technical tokens
    // (dynamic range, video codec, bit depth, audio codec/channels, 3D) are stripped from
    // the title and replaced with tokens generated from the media info.
    public static class VerifiedReleaseTitleBuilder
    {
        private const string Separator = @"[ ._\[\]()\-]";

        private const string DynamicRangeTokens = @"HDR10(?:\+|P(?:lus)?)?|HDR|DV|DoVi|Dolby[ ._]?Vision|HLG(?:10)?|PQ(?:10)?|SDR";
        private const string VideoCodecTokens = @"[xh][ ._]?26[45]|HEVC|AVC|AV1|VC-?1|MPEG-?2|XviD|DivX|VP9";
        private const string BitDepthTokens = @"(?:8|10|12)[ ._-]?bits?|Hi10P?";
        private const string AudioCodecTokens = @"TrueHD|Atmos|DTS(?:[ ._:-]?(?:HD|X|ES|MA|HRA|Express))*|(?:E-?)?AC-?3|DD(?:P|\+)?|DolbyDigital(?:Plus)?|FLAC|L?PCM|(?:HE-?)?AAC(?:-?LC)?|Opus|MP3";
        private const string AudioChannelTokens = @"[1-9][ ._][0-2]";
        private const string ThreeDTokens = @"3D|H?SBS|H?OU";

        private static readonly Regex YearRegex = new(@"(?<=[ ._\[(])(?:19|20)\d{2}(?=[ ._\])])", RegexOptions.Compiled);
        private static readonly Regex DuplicateSeparatorRegex = new(@"[ ._]{2,}", RegexOptions.Compiled);
        private static readonly Regex SeparatorBeforeDashRegex = new(@"[ ._]+-", RegexOptions.Compiled);
        private static readonly Regex EmptyBracketsRegex = new(@"\[[ ._]*\]|\([ ._]*\)", RegexOptions.Compiled);
        private static readonly Regex SeparatorBeforeClosingBracketRegex = new(@"[ ._]+(?=[\])])", RegexOptions.Compiled);

        public static string Build(string title, MediaInfoModel mediaInfo, string releaseGroup = null)
        {
            if (title.IsNullOrWhiteSpace() || mediaInfo == null || mediaInfo.SchemaRevision < VideoFileInfoReader.MINIMUM_MEDIA_INFO_SCHEMA_REVISION)
            {
                return title;
            }

            var extension = string.Empty;
            var titleWithoutExtension = FileExtensions.RemoveFileExtension(title);

            if (titleWithoutExtension.Length < title.Length && title.StartsWith(titleWithoutExtension))
            {
                extension = title.Substring(titleWithoutExtension.Length);
                title = titleWithoutExtension;
            }

            var separator = title.Count(c => c == ' ') > title.Count(c => c == '.') ? " " : ".";

            // Only touch the part after the year so tokens that are part of the movie title survive
            var yearMatch = YearRegex.Match(title);
            var head = yearMatch.Success ? title.Substring(0, yearMatch.Index + yearMatch.Length) : string.Empty;
            var tail = yearMatch.Success ? title.Substring(yearMatch.Index + yearMatch.Length) : title;

            var groupSuffix = string.Empty;

            if (releaseGroup.IsNotNullOrWhiteSpace() && tail.EndsWith("-" + releaseGroup))
            {
                groupSuffix = tail.Substring(tail.Length - releaseGroup.Length - 1);
                tail = tail.Substring(0, tail.Length - groupSuffix.Length);
            }

            var verifiedTokens = GetVerifiedTokens(mediaInfo, title, out var strippedCategories);

            if (strippedCategories.Count > 0)
            {
                var stripRegex = new Regex($@"(?<=^|{Separator})(?:{string.Join("|", strippedCategories)})(?=$|{Separator})", RegexOptions.IgnoreCase);

                tail = stripRegex.Replace(tail, string.Empty);
                tail = EmptyBracketsRegex.Replace(tail, string.Empty);
                tail = DuplicateSeparatorRegex.Replace(tail, separator);
                tail = SeparatorBeforeDashRegex.Replace(tail, "-");
                tail = SeparatorBeforeClosingBracketRegex.Replace(tail, string.Empty);
                tail = tail.TrimEnd(' ', '.', '_', '-');
            }

            if (verifiedTokens.Any())
            {
                tail = tail + separator + string.Join(separator, verifiedTokens);
            }

            return head + tail + groupSuffix + extension;
        }

        private static List<string> GetVerifiedTokens(MediaInfoModel mediaInfo, string sceneName, out List<string> strippedCategories)
        {
            var tokens = new List<string>();
            strippedCategories = new List<string> { DynamicRangeTokens, ThreeDTokens };

            tokens.AddRange(MediaInfoFormatter.FormatVideoDynamicRangeType(mediaInfo).Split(' ', System.StringSplitOptions.RemoveEmptyEntries));

            if (mediaInfo.VideoMultiViewCount > 1)
            {
                tokens.Add("3D");
            }

            var videoCodec = MediaInfoFormatter.FormatVideoCodec(mediaInfo, sceneName);

            if (videoCodec.IsNotNullOrWhiteSpace())
            {
                strippedCategories.Add(VideoCodecTokens);
                tokens.Add(videoCodec);

                if (videoCodec is "x265" or "h265")
                {
                    tokens.Add("HEVC");
                }
                else if (videoCodec is "x264" or "h264")
                {
                    tokens.Add("AVC");
                }
                else if (videoCodec is "HEVC")
                {
                    tokens.Add("x265");
                }
                else if (videoCodec is "AVC")
                {
                    tokens.Add("x264");
                }
            }

            if (mediaInfo.VideoBitDepth > 0)
            {
                strippedCategories.Add(BitDepthTokens);
                tokens.Add($"{mediaInfo.VideoBitDepth}bit");
            }

            var audioCodec = MediaInfoFormatter.FormatAudioCodec(mediaInfo, sceneName);

            if (audioCodec.IsNotNullOrWhiteSpace())
            {
                // Channels glued to the codec (e.g. DDP5.1) are only stripped together with the codec
                strippedCategories.Add($@"(?:{AudioCodecTokens})(?:[ ._]?{AudioChannelTokens})?");
                tokens.AddRange(GetAudioCodecTokens(audioCodec));
            }

            if (mediaInfo.AudioChannels > 0)
            {
                strippedCategories.Add(AudioChannelTokens);
                tokens.Add(FormatAudioChannels(mediaInfo));
            }

            return tokens;
        }

        private static string FormatAudioChannels(MediaInfoModel mediaInfo)
        {
            var channels = mediaInfo.AudioChannelPositions.IsNotNullOrWhiteSpace()
                ? MediaInfoFormatter.FormatAudioChannels(mediaInfo)
                : mediaInfo.AudioChannels switch
                {
                    6 => 5.1m,
                    7 => 6.1m,
                    8 => 7.1m,
                    _ => mediaInfo.AudioChannels
                };

            return channels.ToString("0.0", CultureInfo.InvariantCulture);
        }

        private static IEnumerable<string> GetAudioCodecTokens(string audioCodec)
        {
            return audioCodec switch
            {
                "EAC3 Atmos" => new[] { "DDP", "EAC3", "Atmos" },
                "EAC3" => new[] { "DDP", "EAC3" },
                "AC3" => new[] { "DD", "AC3" },
                "PCM" => new[] { "LPCM" },
                _ => audioCodec.Split(' ')
            };
        }
    }
}
