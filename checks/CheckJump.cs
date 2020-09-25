using MapsetParser.objects;
using MapsetParser.objects.events;
using MapsetParser.objects.hitobjects;
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

namespace JumpDetect.checks
{
    [Check]
    public class CheckJump : BeatmapCheck
    {
        private const int sectionLength = 400;
        public override CheckMetadata GetMetadata() => new BeatmapCheckMetadata()
        {
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
                    new IssueTemplate(Issue.Level.Warning,
                        "{0} Extremely huge spacing ({1}), ensure if it's intended.", "timestamp -", "aimValue")
                    .WithCause("An extremely huge jump, which most of the time is unintended as they're too huge.") },
                { "Warn",
                    new IssueTemplate(Issue.Level.Warning,
                        "{0} Abnormally huge spacing ({1}), ensure if it's intended.", "timestamp -", "aimValue")
                    .WithCause("Probably a big jump, though it may be intended.") },
                { "Minor",
                    new IssueTemplate(Issue.Level.Minor,
                        "{0} Spacing is above average ({1}), though this is most likely fine.", "timestamp -", "aimValue")
                    .WithCause("Most likely doesn't matter, but it's a jump above average.") }
            };
        }
        public override IEnumerable<Issue> GetIssues(Beatmap beatmap)
        {
            List<StrainObject> strainObjects = new List<StrainObject>();
            ObjectAim aimSkill = new ObjectAim();
            foreach (HitObject hitObject in beatmap.hitObjects.Skip(1))
            {
                var strainValue = aimSkill.GetStrain(hitObject);
                strainObjects.Add(new StrainObject(hitObject, strainValue));
            }

            strainObjects.Sort((x, y) => x.StrainValue.CompareTo(y.StrainValue));
            var avg = strainObjects.Average(x => x.StrainValue);
            var zooom = strainObjects
                .Where(x => x.StrainValue > avg)
                .TakeLast(10);
            double previousStrain = 0.0;
            foreach (var obj in zooom)
            {
                if (previousStrain == 0.0)
                    previousStrain = obj.StrainValue;

                var deltaStrain = obj.StrainValue - previousStrain;
                if (deltaStrain >= 1)
                    yield return new Issue(GetTemplate("Prob"), beatmap, Timestamp.Get(obj.MapObject), obj.StrainValue);
                else if (deltaStrain >= 0.5)
                    yield return new Issue(GetTemplate("Warn"), beatmap, Timestamp.Get(obj.MapObject), obj.StrainValue);
                else
                    yield return new Issue(GetTemplate("Minor"), beatmap, Timestamp.Get(obj.MapObject), obj.StrainValue);
                previousStrain = obj.StrainValue;
            }

        }
    }

    public class StrainObject
    {
        public StrainObject(HitObject mapObject, double strainValue)
        {
            MapObject = mapObject;
            StrainValue = strainValue;
        }
        public virtual HitObject MapObject { get; private set; }
        public virtual double StrainValue { get; private set; }
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
