using System.Collections.Generic;
using DLS.Description;
using Seb.Types;

namespace DLS.Game
{
	public class DisplayInstance : IClickable
	{
		public List<DisplayInstance> ChildDisplays;
		public DisplayDescription Desc;
		public ChipType DisplayType;
		public Bounds2D LastDrawBounds;
	}
}