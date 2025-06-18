using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;

namespace Epi.Display.Lg
{
    public class LgDisplayBridgeJoinMap : DisplayControllerJoinMap
    {
        #region Digitals

        //[JoinName("PowerOff")]
        //public JoinDataComplete PowerOff = new JoinDataComplete(
        //    new JoinData
        //    {
        //        JoinNumber = 1,
        //        JoinSpan = 1
        //    },
        //    new JoinMetadata
        //    {
        //        Description = "Power Off",
        //        JoinCapabilities = eJoinCapabilities.FromSIMPL,
        //        JoinType = eJoinType.Digital
        //    });

        //[JoinName("PowerOn")]
        //public JoinDataComplete PowerOn = new JoinDataComplete(
        //    new JoinData
        //    {
        //        JoinNumber = 2,
        //        JoinSpan = 1
        //    },
        //    new JoinMetadata
        //    {
        //        Description = "Power On",
        //        JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
        //        JoinType = eJoinType.Digital
        //    });

        //[JoinName("IsTwoWayDisplay")]
        //public JoinDataComplete IsTwoWayDisplay = new JoinDataComplete(
        //    new JoinData
        //    {
        //        JoinNumber = 3,
        //        JoinSpan = 1
        //    },
        //    new JoinMetadata
        //    {
        //        Description = "Is Two Way Display",
        //        JoinCapabilities = eJoinCapabilities.ToSIMPL,
        //        JoinType = eJoinType.Digital
        //    });

        //[JoinName("InputSelectOffset")]
        //public JoinDataComplete InputSelectOffset = new JoinDataComplete(
        //    new JoinData
        //    {
        //        JoinNumber = 11,
        //        JoinSpan = 10
        //    },
        //    new JoinMetadata
        //    {
        //        Description = "Input Select",
        //        JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
        //        JoinType = eJoinType.Digital
        //    });

        //[JoinName("ButtonVisibilityOffset")]
        //public JoinDataComplete ButtonVisibilityOffset = new JoinDataComplete(
        //    new JoinData
        //    {
        //        JoinNumber = 41,
        //        JoinSpan = 10
        //    },
        //    new JoinMetadata
        //    {
        //        Description = "Button Visibility Offset",
        //        JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
        //        JoinType = eJoinType.DigitalSerial
        //    });

        //[JoinName("IsOnline")]
        //public JoinDataComplete IsOnline = new JoinDataComplete(
        //    new JoinData
        //    {
        //        JoinNumber = 50,
        //        JoinSpan = 1
        //    },
        //    new JoinMetadata
        //    {
        //        Description = "Is Online",
        //        JoinCapabilities = eJoinCapabilities.ToSIMPL,
        //        JoinType = eJoinType.Digital
        //    });

        #endregion


        #region Analogs

        //[JoinName("InputSelect")]
        //public JoinDataComplete InputSelect = new JoinDataComplete(
        //    new JoinData
        //    {
        //        JoinNumber = 11,
        //        JoinSpan = 1
        //    },
        //    new JoinMetadata
        //    {
        //        Description = "Input Select",
        //        JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
        //        JoinType = eJoinType.Analog
        //    });

        #endregion


        #region Serials

        //[JoinName("Name")]
        //public JoinDataComplete Name = new JoinDataComplete(
        //    new JoinData
        //    {
        //        JoinNumber = 1,
        //        JoinSpan = 1
        //    },
        //    new JoinMetadata
        //    {
        //        Description = "Name",
        //        JoinCapabilities = eJoinCapabilities.ToSIMPL,
        //        JoinType = eJoinType.Serial
        //    });

        //[JoinName("InputNamesOffset")]
        //public JoinDataComplete InputNamesOffset = new JoinDataComplete(
        //    new JoinData
        //    {
        //        JoinNumber = 11,
        //        JoinSpan = 10
        //    },
        //    new JoinMetadata
        //    {
        //        Description = "Input Names Offset",
        //        JoinCapabilities = eJoinCapabilities.ToSIMPL,
        //        JoinType = eJoinType.Serial
        //    });

        #endregion


        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="joinStart"></param>
        public LgDisplayBridgeJoinMap(uint joinStart)
            : base(joinStart, typeof(LgDisplayBridgeJoinMap))
        {

        }
}
}