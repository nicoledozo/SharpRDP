using System.Collections.Generic;

namespace SharpRDP
{
    public class Layout
    {
        public string Id { get; set; }
        public Dictionary<string, Code> Keycode { get; set; }
    }
}
