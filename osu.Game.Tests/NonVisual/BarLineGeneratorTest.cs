// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Collections.Generic;
using NUnit.Framework;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Timing;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Tests.NonVisual
{
    public class BarLineGeneratorTest
    {
        [Test]
        public void TestRoundingErrorCompensation()
        {
            // The aim of this test is to make sure bar line generation compensates for floating-point errors.
            // The premise of the test is that we have a single timing point that should result in bar lines
            // that start at a time point that is a whole number every seventh beat.

            // The fact it's every seventh beat is important - it's a number indivisible by 2, which makes
            // it susceptible to rounding inaccuracies. In fact this was originally spotted in cases of maps
            // that met exactly this criteria.

            const int beat_length_numerator = 2000;
            const int beat_length_denominator = 7;
            TimeSignature signature = TimeSignature.SimpleQuadruple;

            var beatmap = new Beatmap
            {
                HitObjects = new List<HitObject>
                {
                    new HitObject { StartTime = 0 },
                    new HitObject { StartTime = 120_000 }
                },
                ControlPointInfo = new ControlPointInfo()
            };

            beatmap.ControlPointInfo.Add(0, new TimingControlPoint
            {
                BeatLength = (double)beat_length_numerator / beat_length_denominator,
                TimeSignature = signature
            });

            var barLines = new BarLineGenerator<BarLine>(beatmap).BarLines;

            for (int i = 0; i * beat_length_denominator < barLines.Count; i++)
            {
                var barLine = barLines[i * beat_length_denominator];
                int expectedTime = beat_length_numerator * signature.Numerator * i;

                // every seventh bar's start time should be at least greater than the whole number we expect.
                // It cannot be less, as that can affect overlapping scroll algorithms
                // (the previous timing point might be chosen incorrectly if this is not the case)
                Assert.GreaterOrEqual(barLine.StartTime, expectedTime);

                // on the other side, make sure we don't stray too far from the expected time either.
                Assert.IsTrue(Precision.AlmostEquals(barLine.StartTime, expectedTime));

                // check major/minor lines for good measure too
                Assert.AreEqual(i % signature.Numerator == 0, barLine.Major);
            }
        }

        [Test]
        public void TestOmitBarLineSimple()
        {
            // Simple test to omit the second bar line.

            const int beat_length = 500;
            TimeSignature signature = TimeSignature.SimpleQuadruple;
            int next_barline_start = beat_length * signature.Numerator;

            var beatmap = new Beatmap
            {
                HitObjects = new List<HitObject>
                {
                    new HitObject { StartTime = 0 },
                    new HitObject { StartTime = 120_000 }
                },
                ControlPointInfo = new ControlPointInfo()
            };

            beatmap.ControlPointInfo.Add(0, new TimingControlPoint
            {
                BeatLength = beat_length,
                TimeSignature = signature
            });

            beatmap.ControlPointInfo.Add(next_barline_start, new TimingControlPoint
            {
                BeatLength = beat_length,
                TimeSignature = signature
            });
            beatmap.ControlPointInfo.Add(next_barline_start, new EffectControlPoint
            {
                OmitFirstBarLine = true
            });

            var barLines = new BarLineGenerator<BarLine>(beatmap).BarLines;

            // first line should exist and be major
            Assert.IsTrue(Precision.AlmostEquals(0, barLines[0].StartTime));
            Assert.IsTrue(barLines[0].Major);

            // a timing + control point to omit the bar line was set where the second bar line of the first control point should be,
            // so the actual second barline should not be at that time
            Assert.IsFalse(Precision.AlmostEquals(barLines[1].StartTime, next_barline_start));

            // check major/minor lines for good measure;
            // because there was one line from the first control point but the first line of the second control point was omitted,
            // we can do this check as if no lines were added/removed at all
            for (int i = 1; i < barLines.Count; i++)
            {
                Assert.IsTrue(barLines[i].Major == (i % signature.Numerator == 0));
            }
        }

        [Test]
        public void TestOmitBarLineRounding()
        {
            const int beat_length_numerator = 2000;
            const int beat_length_denominator = 7;
            const int omit_barline_end_mark = 60_000;
            const double beat_length = (double)beat_length_numerator / beat_length_denominator;
            TimeSignature signature = TimeSignature.SimpleQuadruple;
            double bar_length = System.Math.Round(beat_length * signature.Numerator, System.MidpointRounding.AwayFromZero);

            var beatmap = new Beatmap
            {
                HitObjects = new List<HitObject>
                {
                    new HitObject { StartTime = 0 },
                    new HitObject { StartTime = 120_000 }
                },
                ControlPointInfo = new ControlPointInfo()
            };

            beatmap.ControlPointInfo.Add(0, new TimingControlPoint
            {
                BeatLength = beat_length,
                TimeSignature = signature
            });

            // up to an specific time mark, a timing + effect control point to omit bar line is added
            // at every point where a barline should normally be placed.
            for (double t = bar_length; Precision.DefinitelyBigger(omit_barline_end_mark, t); t += bar_length)
            {
                beatmap.ControlPointInfo.Add(t, new TimingControlPoint
                {
                    BeatLength = beat_length,
                    TimeSignature = signature
                });

                beatmap.ControlPointInfo.Add(t, new EffectControlPoint
                {
                    OmitFirstBarLine = true
                });
            }

            var barLines = new BarLineGenerator<BarLine>(beatmap).BarLines;

            // first line should exist and be major
            Assert.IsTrue(Precision.AlmostEquals(0, barLines[0].StartTime));
            Assert.IsTrue(barLines[0].Major);

            // other that the first line, no other line should be before the set time mark;
            // check that the next barlines are after such time mark
            for (int i = 1; i < barLines.Count; i++)
            {
                Assert.IsTrue(Precision.DefinitelyBigger(barLines[i].StartTime, omit_barline_end_mark));
                Assert.IsTrue(barLines[i].Major == (i % signature.Numerator == 0));
            }
        }

        private class BarLine : IBarLine
        {
            public double StartTime { get; set; }
            public bool Major { get; set; }
        }
    }
}
