using System;
using System.Collections.Generic;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;

namespace PepperDash.Essentials.Plugins.Lg.Display
{
    public class LgDisplayIrInputs : ISelectableItems<string>
    {
        private Dictionary<string, ISelectableItem> items = new Dictionary<string, ISelectableItem>();

        public Dictionary<string, ISelectableItem> Items
        {
            get
            {
                return items;
            }
            set
            {
                if (items == value)
                    return;

                items = value;

                ItemsUpdated?.Invoke(this, null);
            }
        }

        private string currentItem;

        public string CurrentItem
        {
            get
            {
                return currentItem;
            }
            set
            {
                if (currentItem == value)
                    return;

                currentItem = value;

                CurrentItemChanged?.Invoke(this, null);
            }
        }

        public event EventHandler ItemsUpdated;
        public event EventHandler CurrentItemChanged;

    }

    public class LgDisplayIrInput : ISelectableItem
    {
        private bool isSelected;

        private readonly LgDisplayIrController parent;

        public LgDisplayIrInput(string key, string name, LgDisplayIrController parent)
        {
            Key = key;
            Name = name;
            this.parent = parent;
        }

        public string Key { get; private set; }
        public string Name { get; private set; }

        public event EventHandler ItemUpdated;

        public bool IsSelected
        {
            get { return isSelected; }
            set
            {
                if (value == isSelected)
                    return;

                isSelected = value;
                ItemUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Select()
        {
            //_parent.SendData($"xb {_parent.Id} {Key}");
            // TODO - Implement IR Input Selection
            parent.ExecuteSwitch(Key);
        }
    }
}
