using WebApi.Models.Collections;

namespace WebApi.Models
{
    public class Foo
    {
        public CustomList<Bar> Bars { get; set; } = [];

        public CustomList<Foo> Foos { get; set; } = [];
    }
}