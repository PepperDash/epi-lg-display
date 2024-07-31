using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core;

namespace Epi.Display.Lg
{
	public class LgDisplayJoinMap : DisplayControllerJoinMap
	{
		public LgDisplayJoinMap(uint joinStart)
			: base(joinStart, typeof(LgDisplayJoinMap))
		{
        }
		[JoinName("VideoMuteOff")]
		public JoinDataComplete VideoMuteOff =
			new JoinDataComplete(new JoinData { JoinNumber = 21, JoinSpan = 1 },
			new JoinMetadata
			{
				Description = "VideoMuteOff",
				JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
				JoinType = eJoinType.Digital
			});
		[JoinName("VideoMuteOn")]
		public JoinDataComplete VideoMuteOn =
			new JoinDataComplete(new JoinData { JoinNumber = 22, JoinSpan = 1 },
			new JoinMetadata
			{
				Description = "VideoMuteOn",
				JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
				JoinType = eJoinType.Digital
			});
		[JoinName("VideoMuteToggle")]
		public JoinDataComplete VideoMuteToggle =
			new JoinDataComplete(new JoinData { JoinNumber = 23, JoinSpan = 1 },
			new JoinMetadata
			{
				Description = "VideoMuteOn",
				JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
				JoinType = eJoinType.Digital
			});
	}
}