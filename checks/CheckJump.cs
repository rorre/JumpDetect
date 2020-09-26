using MapsetParser.objects;
using MapsetParser.objects.events;
using MapsetParser.objects.hitobjects;
using MapsetParser.objects.timinglines;
using MapsetParser.statics;
using MapsetParser.starrating.standard;
using MapsetVerifierFramework;
using MapsetVerifierFramework.objects;
using MapsetVerifierFramework.objects.attributes;
using MapsetVerifierFramework.objects.metadata;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using JumpDetect.helper;

namespace JumpDetect.checks
{
    [Check]
    public class CheckJump : BeatmapCheck
    {
        private const int sectionLength = 400;
        public override CheckMetadata GetMetadata() => new BeatmapCheckMetadata()
        {
            Modes = new Beatmap.Mode[]
            {
                Beatmap.Mode.Standard
            },
            Category = "Compose",
            Message = "Abnormally huge spacing.",
            Author = "-Keitaro",

            Documentation = new Dictionary<string, string>()
            {
                {
                    "Purpose",
                    @"
                    Prevent unintentional huge spacing spike.
                    <image-right>
                        https://d.rorre.xyz/ksyOI1ZMP/screenshot1745.jpg
                        An extremely huge spacing in a map (1,2) that could cause unexpected gameplay experience.
                    </image>"
                },
                {
                    "Reasoning",
                    @"
                    If not done intentionally, it will cause gameplay to suffer by doing huge jump all of the sudden.
                    <note>
                        aimValue in this case is old star rating's aim weight for an object. 
                        While it is outdated, it should be able to represent such huge spacing accordingly.
                    </note>"
                }
            }
        };

        public override Dictionary<string, IssueTemplate> GetTemplates()
        {
            return new Dictionary<string, IssueTemplate>()
            {
                { "Prob",
                    new IssueTemplate(Issue.Level.Problem,
                        "{0} Extremely huge ({1}) spacing ({2}), ensure if it's intended.", "timestamp -", "snapping", "aimValue")
                    .WithCause("An extremely huge jump, which most of the time is unintended as they're too huge.") },
                { "Warn",
                    new IssueTemplate(Issue.Level.Warning,
                        "{0} Abnormally huge ({1}) spacing ({2}), ensure if it's intended.", "timestamp -", "snapping", "aimValue")
                    .WithCause("Probably a big jump, though it may be intended.") },
                { "Minor",
                    new IssueTemplate(Issue.Level.Minor,
                        "{0} Spacing is above average for ({1}) snap ({2}), though this is most likely fine.", "snapping", "timestamp -", "aimValue")
                    .WithCause("Most likely doesn't matter, but it's a jump above average.") }
            };
        }
        public override IEnumerable<Issue> GetIssues(Beatmap beatmap)
        {
            List<StrainObject> strainObjects = new List<StrainObject>();
            ObjectAim aimSkill = new ObjectAim();
            foreach (HitObject hitObject in beatmap.hitObjects.Skip(1))
            {
                var strainValue = Math.Round(aimSkill.GetStrain(hitObject), 2);
                var snapping = GetSnappingGap(beatmap, hitObject);
                strainObjects.Add(
                    new StrainObject
                    {
                        MapObject = hitObject,
                        StrainValue = strainValue,
                        Snapping = snapping
                    }
                );
            }

            var groupedSnap = strainObjects.GroupBy(x => x.Snapping);
            foreach (var snappingGroup in groupedSnap)
                foreach (var issue in GetHugeJumps(beatmap, snappingGroup))
                    yield return issue;

        }

        private string GetSnappingGap(Beatmap beatmap, HitObject hitObject)
        {
            HitObject previousObject = hitObject.PrevOrFirst();
            double lastObjectTime = previousObject.GetEdgeTimes().Last();
            double snappedCurrentObject = hitObject.time + beatmap.GetPracticalUnsnap(hitObject.time);
            double snappedPreviousObject = lastObjectTime + beatmap.GetPracticalUnsnap(lastObjectTime);
            double deltaTime = snappedCurrentObject - snappedPreviousObject;

            UninheritedLine timingLine = beatmap.GetTimingLine<UninheritedLine>(snappedCurrentObject);

            var snapping = Math.Round(deltaTime / timingLine.msPerBeat, 2);
            var snappingStr = new Fraction(snapping).ToString();
            return snappingStr;

        }
        private IEnumerable<Issue> GetHugeJumps(Beatmap beatmap, IGrouping<string, StrainObject> strainObjects)
        {
            var sortedStrain = strainObjects.ToList();
            sortedStrain.Sort((x, y) => x.StrainValue.CompareTo(y.StrainValue));

            var biggestJumps = sortedStrain.TakeLast(10).ToList();
            double previousStrain = 0.0;

            if (biggestJumps.Count < 5)
                yield break;

            for (var i = 0; i < biggestJumps.Count; i++)
            {
                StrainObject currentObject = biggestJumps[i];
                StrainObject nextHighest = null;
                StrainObject nextNextHighest = null;
                if (i + 1 < biggestJumps.Count)
                    nextHighest = biggestJumps[i + 1];

                if (i + 2 < biggestJumps.Count)
                    nextNextHighest = biggestJumps[i + 2];

                if (previousStrain == 0.0)
                    previousStrain = currentObject.StrainValue;
                var currentStrain = currentObject.StrainValue;
                var nextStrain = nextHighest != null ? nextHighest.StrainValue : double.MaxValue;
                var nextNextStrain = nextNextHighest != null ? nextNextHighest.StrainValue : double.MaxValue;

                var deltaStrain = currentStrain - previousStrain;
                var nextNextDeltaStrain = nextNextStrain - nextStrain;

                previousStrain = currentStrain;
                if (nextNextDeltaStrain < 0.75 && nextStrain < 0.75)
                    continue;

                if (deltaStrain >= 1.5)
                    yield return new Issue(
                        GetTemplate("Prob"),
                        beatmap,
                        Timestamp.Get(currentObject.MapObject.Prev(), currentObject.MapObject),
                        strainObjects.Key,
                        currentStrain
                    );
                else if (deltaStrain >= 0.75)
                    yield return new Issue(
                        GetTemplate("Warn"),
                        beatmap,
                        Timestamp.Get(currentObject.MapObject.Prev(), currentObject.MapObject),
                        strainObjects.Key,
                        currentStrain
                    );
                else if (deltaStrain >= 0.5 && i != biggestJumps.Count - 1)
                    yield return new Issue(
                        GetTemplate("Minor"),
                        beatmap,
                        Timestamp.Get(currentObject.MapObject.Prev(), currentObject.MapObject),
                        strainObjects.Key,
                        currentStrain
                    );
            }
        }
    }

    public class StrainObject
    {
        public HitObject MapObject { get; set; }
        public double StrainValue { get; set; }
        public string Snapping { get; set; }
    }

    public class ObjectAim : Aim
    {
        public double GetStrain(HitObject obj)
        {
            if (obj is Spinner)
                return 0;
            return StrainValueOf(obj);
        }
    }
}
