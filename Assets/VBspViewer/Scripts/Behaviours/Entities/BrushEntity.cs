using VBspViewer.Importing.Entities;

namespace VBspViewer.Behaviours.Entities
{
    [ClassName(HammerName = "func_brush")]
    public class BrushEntity : BaseEntity
    {
        public int ModelIndex;

        protected override void OnKeyVal(string key, EntValue val)
        {
            switch (key)
            {
                case "model":
                    var valStr = (string) val;
                    if (valStr.StartsWith("*")) ModelIndex = int.Parse(valStr.Substring(1));
                    break;
                default:
                    base.OnKeyVal(key, val);
                    break;
            }
        }
    }
}
