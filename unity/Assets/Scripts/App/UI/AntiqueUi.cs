using UnityEngine.UIElements;

namespace Sisifos.App.UI
{
    public static partial class Antique
    {
        /// <summary>the menu's frame as an element (see FramePaint)</summary>
        public static CanvasBox Frame(double seed)
        {
            return new CanvasBox((c, w, h) => FramePaint(c, w, h, seed)) { pickingMode = PickingMode.Ignore };
        }
    }
}
