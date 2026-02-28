// Traffic Simulation
// https://github.com/mchrbn/unity-traffic-simulation

using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TrafficSimulation {
    [System.Serializable]
    public class LaneCounterUIEntry {
        public LaneSensor sensor;
        public TMP_Text text;
        public string label;
    }

    public class LaneCounterUI : MonoBehaviour {
        public string format = "{0}: {1}";
        public List<LaneCounterUIEntry> entries = new List<LaneCounterUIEntry>();

        void Update(){
            foreach(LaneCounterUIEntry entry in entries){
                if(entry == null || entry.text == null || entry.sensor == null)
                    continue;

                if(string.IsNullOrEmpty(entry.label))
                    entry.text.text = entry.sensor.VehicleCount.ToString();
                else
                    entry.text.text = string.Format(format, entry.label, entry.sensor.VehicleCount);
            }
        }
    }
}
