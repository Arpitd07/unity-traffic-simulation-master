// Traffic Simulation
// https://github.com/mchrbn/unity-traffic-simulation

using System.Collections.Generic;
using UnityEngine;

namespace TrafficSimulation {
    public class LaneSensor : MonoBehaviour {
        private readonly HashSet<GameObject> vehicles = new HashSet<GameObject>();

        public int VehicleCount => vehicles.Count;

        void OnTriggerEnter(Collider other)
        {
            if (other == null || other.tag != "AutonomousVehicle")
                return;

            vehicles.Add(other.gameObject);

            Debug.Log($"[LaneSensor {gameObject.name}] Vehicle Entered. Count: {VehicleCount}");
        }

        void OnTriggerExit(Collider other)
        {
            if (other == null || other.tag != "AutonomousVehicle")
                return;

            vehicles.Remove(other.gameObject);

            Debug.Log($"[LaneSensor {gameObject.name}] Vehicle Exited. Count: {VehicleCount}");
        }
    }
}
