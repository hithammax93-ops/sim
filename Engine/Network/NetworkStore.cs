using System;
using System.Collections.Generic;
using HmaxUnlocker.Engine.Models;

namespace HmaxUnlocker.Engine.Network
{
    public class NetworkStore
    {
        private readonly List<NetworkEntry> _selectedNetworks = new List<NetworkEntry>();

        public IReadOnlyList<NetworkEntry> SelectedNetworks => _selectedNetworks.AsReadOnly();

        public void AddNetwork(NetworkEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (!_selectedNetworks.Contains(entry))
                _selectedNetworks.Add(entry);
        }

        public void RemoveNetwork(NetworkEntry entry)
        {
            if (entry != null)
                _selectedNetworks.Remove(entry);
        }

        public void Clear()
        {
            _selectedNetworks.Clear();
        }

        public string GetSelectedSummary()
        {
            return GetSummary();
        }

        public string GetSummary()
        {
            if (_selectedNetworks.Count == 0) return "None";
            var parts = new List<string>();
            foreach (var n in _selectedNetworks)
                parts.Add(n.NetworkName + " (" + n.Mcc + "/" + n.Mnc + ")");
            return string.Join(", ", parts);
        }
    }
}
