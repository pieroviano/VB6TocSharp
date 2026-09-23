using System.Collections.Generic;
using System.Linq;
using Extras.Model;

// Two record types that share a simple name and differ only by namespace: the field-info cache
// must not confuse them.
namespace Extras.Tests.Fixtures.Left
{
    public class Ambiguous : FieldInfoListSource
    {
        [RecordField(max: 1)] public string OnlyOnTheLeft = "";

        public List<string> Names() => FieldInfoList().Select(f => f.Name).ToList();
    }
}

namespace Extras.Tests.Fixtures.Right
{
    public class Ambiguous : FieldInfoListSource
    {
        [RecordField(max: 1)] public string OnlyOnTheRight = "";
        [RecordField(max: 1)] public string AndASecondOne = "";

        public List<string> Names() => FieldInfoList().Select(f => f.Name).ToList();
    }
}
