using XmlSerializerBugRepro.Models.Collections;

namespace XmlSerializerBugRepro.Models
{
    public class Foo
    {
        public CustomList<Bar> Bars { get; set; } = [];

        public CustomList<Foo> Foos { get; set; } = [];
    }
}