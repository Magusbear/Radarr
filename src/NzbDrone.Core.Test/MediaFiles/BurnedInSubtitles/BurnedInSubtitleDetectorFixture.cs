using System;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles.BurnedInSubtitles;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaFiles.BurnedInSubtitles
{
    [TestFixture]
    public class BurnedInSubtitleDetectorFixture : CoreTest<BurnedInSubtitleDetector>
    {
        private const string Header = "level\tpage_num\tblock_num\tpar_num\tline_num\tword_num\tleft\ttop\twidth\theight\tconf\ttext";
        private const string ChineseSubtitle = Header + "\n" +
                                               "1\t1\t0\t0\t0\t0\t0\t0\t1920\t300\t-1\t\n" +
                                               "5\t1\t1\t1\t1\t1\t700\t200\t300\t60\t91.5\t我们必须\n" +
                                               "5\t1\t1\t1\t1\t2\t1000\t200\t200\t60\t88.2\t离开这里\n";
        private const string Noise = Header + "\n" +
                                     "5\t1\t1\t1\t1\t1\t10\t10\t30\t30\t21.4\t县\n" +
                                     "5\t1\t1\t1\t1\t2\t50\t10\t30\t30\t35.0\t一\n";
        private const string Empty = Header + "\n";

        private static readonly TimeSpan Runtime = TimeSpan.FromMinutes(120);

        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<ISubtitleFrameReader>().Setup(r => r.IsAvailable()).Returns(true);
        }

        private void GivenFrames(int framesWithSubtitles, string otherFrames = Empty)
        {
            var frame = 0;

            Mocker.GetMock<ISubtitleFrameReader>()
                .Setup(r => r.ReadSubtitleArea(It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Returns(() => frame++ < framesWithSubtitles ? ChineseSubtitle : otherFrames);
        }

        [Test]
        public void should_count_confident_chinese_characters()
        {
            BurnedInSubtitleDetector.CountChineseCharacters(ChineseSubtitle).Should().Be(8);
        }

        [Test]
        public void should_ignore_low_confidence_characters()
        {
            BurnedInSubtitleDetector.CountChineseCharacters(Noise).Should().Be(0);
        }

        [Test]
        public void should_ignore_latin_text()
        {
            BurnedInSubtitleDetector.CountChineseCharacters(Header + "\n5\t1\t1\t1\t1\t1\t0\t0\t10\t10\t95\tHello\n").Should().Be(0);
        }

        [Test]
        public void should_detect_subtitles_in_enough_frames()
        {
            GivenFrames(BurnedInSubtitleDetector.MinimumFramesWithText);

            var result = Subject.DetectChinese("/movie.mkv", Runtime);

            result.Status.Should().Be(BurnedInSubtitleStatus.Detected);
            result.SampledFrames.Should().Be(BurnedInSubtitleDetector.SampleCount);
            result.FramesWithText.Should().Be(BurnedInSubtitleDetector.MinimumFramesWithText);
        }

        [Test]
        public void should_not_detect_text_in_few_frames()
        {
            GivenFrames(BurnedInSubtitleDetector.MinimumFramesWithText - 1, Noise);

            Subject.DetectChinese("/movie.mkv", Runtime).Status.Should().Be(BurnedInSubtitleStatus.NotDetected);
        }

        [Test]
        public void should_sample_frames_between_intro_and_credits()
        {
            GivenFrames(0);

            Subject.DetectChinese("/movie.mkv", Runtime);

            Mocker.GetMock<ISubtitleFrameReader>()
                .Verify(r => r.ReadSubtitleArea("/movie.mkv", It.Is<TimeSpan>(t => t < Runtime * 0.1 || t > Runtime * 0.9)), Times.Never());
            Mocker.GetMock<ISubtitleFrameReader>()
                .Verify(r => r.ReadSubtitleArea("/movie.mkv", It.IsAny<TimeSpan>()), Times.Exactly(BurnedInSubtitleDetector.SampleCount));
        }

        [Test]
        public void should_be_unknown_when_tools_are_not_available()
        {
            Mocker.GetMock<ISubtitleFrameReader>().Setup(r => r.IsAvailable()).Returns(false);

            Subject.DetectChinese("/movie.mkv", Runtime).Status.Should().Be(BurnedInSubtitleStatus.Unknown);
        }

        [Test]
        public void should_be_unknown_when_most_frames_cannot_be_read()
        {
            Mocker.GetMock<ISubtitleFrameReader>()
                .Setup(r => r.ReadSubtitleArea(It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Returns((string)null);

            Subject.DetectChinese("/movie.mkv", Runtime).Status.Should().Be(BurnedInSubtitleStatus.Unknown);
        }
    }
}
