// Traffic Simulation
// https://github.com/mchrbn/unity-traffic-simulation

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TrafficSimulation{
    public enum IntersectionType{
        STOP,
        TRAFFIC_LIGHT
    }

    public class Intersection : MonoBehaviour
    {   
        public IntersectionType intersectionType;
        public int id;  

        //For stop only
        public List<Segment> prioritySegments;

        //For traffic lights only
        public float lightsDuration = 8;
        public float orangeLightDuration = 2;
        public List<Segment> lightsNbr1;
        public List<Segment> lightsNbr2;
        public float minGreenTime = 5f;
        public float maxGreenTime = 25f;
        public float timePerVehicle = 1f;
        public List<LaneSensor> group1Sensors = new List<LaneSensor>();
        public List<LaneSensor> group2Sensors = new List<LaneSensor>();

        private List<GameObject> vehiclesQueue;
        private List<GameObject> vehiclesInIntersection;
        private TrafficSystem trafficSystem;
        private float group1WaitingTime;
        private float group2WaitingTime;
        private int lastGreenGroup = 2;
        private Coroutine trafficLightRoutine;
        
        [HideInInspector] public int currentRedLightsGroup = 1;

        void Start()
        {
            vehiclesQueue = new List<GameObject>();
            vehiclesInIntersection = new List<GameObject>();

            if (intersectionType == IntersectionType.TRAFFIC_LIGHT)
            {
                group1WaitingTime = 0f;
                group2WaitingTime = 0f;
                lastGreenGroup = currentRedLightsGroup == 1 ? 2 : 1;

                Debug.Log($"[Intersection {id}] Adaptive Traffic System Initialized.");
                Debug.Log($"[Intersection {id}] Starting Red Group: {currentRedLightsGroup}");

                trafficLightRoutine = StartCoroutine(AdaptiveLightsLoop());
            }
        }

        void Update(){
            if(intersectionType != IntersectionType.TRAFFIC_LIGHT)
                return;

            if(currentRedLightsGroup == 1)
                group1WaitingTime += Time.deltaTime;
            else
                group2WaitingTime += Time.deltaTime;
        }

        void SwitchLights()
        {

            if(currentRedLightsGroup == 1) currentRedLightsGroup = 2;
            else if(currentRedLightsGroup == 2) currentRedLightsGroup = 1;            
            
            //Wait few seconds after light transition before making the other car move (= orange light)
            Invoke("MoveVehiclesQueue", orangeLightDuration);
        }

        IEnumerator AdaptiveLightsLoop()
        {
            while (true)
            {

                int nextGreenGroup = SelectNextGreenGroup();

                Debug.Log($"[Intersection {id}] Selected Green Group: {nextGreenGroup}");

                SetGreenGroup(nextGreenGroup);

                float vehicleCount = nextGreenGroup == 1
                    ? GetGroupVehicleCount(group1Sensors)
                    : GetGroupVehicleCount(group2Sensors);

                float greenTime = Mathf.Clamp(
                    vehicleCount * timePerVehicle,
                    minGreenTime,
                    maxGreenTime
                );

                if (vehicleCount <= 0f)
                    greenTime = minGreenTime;

                Debug.Log($"[Intersection {id}] VehicleCount: {vehicleCount}");
                Debug.Log($"[Intersection {id}] Calculated Green Time: {greenTime} seconds");

                lastGreenGroup = nextGreenGroup;

                yield return new WaitForSeconds(orangeLightDuration);

                Debug.Log($"[Intersection {id}] Orange phase complete. Releasing vehicles.");

                MoveVehiclesQueue();

                float remaining = Mathf.Max(0f, greenTime - orangeLightDuration);
                yield return new WaitForSeconds(remaining);

                Debug.Log($"[Intersection {id}] Green phase completed for Group {nextGreenGroup}");
            }
        }

        void OnTriggerEnter(Collider _other) {
            //Check if vehicle is already in the list if yes abort
            //Also abort if we just started the scene (if vehicles inside colliders at start)
            if(IsAlreadyInIntersection(_other.gameObject) || Time.timeSinceLevelLoad < .5f) return;

            if(_other.tag == "AutonomousVehicle" && intersectionType == IntersectionType.STOP)
                TriggerStop(_other.gameObject);
            else if(_other.tag == "AutonomousVehicle" && intersectionType == IntersectionType.TRAFFIC_LIGHT)
                TriggerLight(_other.gameObject);
        }

        void OnTriggerExit(Collider _other) {
            if(_other.tag == "AutonomousVehicle" && intersectionType == IntersectionType.STOP)
                ExitStop(_other.gameObject);
            else if(_other.tag == "AutonomousVehicle" && intersectionType == IntersectionType.TRAFFIC_LIGHT)
                ExitLight(_other.gameObject);
        }

        void TriggerStop(GameObject _vehicle){
            VehicleAI vehicleAI = _vehicle.GetComponent<VehicleAI>();
            
            //Depending on the waypoint threshold, the car can be either on the target segment or on the past segment
            int vehicleSegment = vehicleAI.GetSegmentVehicleIsIn();

            if(!IsPrioritySegment(vehicleSegment)){
                if(vehiclesQueue.Count > 0 || vehiclesInIntersection.Count > 0){
                    vehicleAI.vehicleStatus = Status.STOP;
                    vehiclesQueue.Add(_vehicle);
                }
                else{
                    vehiclesInIntersection.Add(_vehicle);
                    vehicleAI.vehicleStatus = Status.SLOW_DOWN;
                }
            }
            else{
                vehicleAI.vehicleStatus = Status.SLOW_DOWN;
                vehiclesInIntersection.Add(_vehicle);
            }
        }

        void ExitStop(GameObject _vehicle){

            _vehicle.GetComponent<VehicleAI>().vehicleStatus = Status.GO;
            vehiclesInIntersection.Remove(_vehicle);
            vehiclesQueue.Remove(_vehicle);

            if(vehiclesQueue.Count > 0 && vehiclesInIntersection.Count == 0){
                vehiclesQueue[0].GetComponent<VehicleAI>().vehicleStatus = Status.GO;
            }
        }

        void TriggerLight(GameObject _vehicle){
            VehicleAI vehicleAI = _vehicle.GetComponent<VehicleAI>();
            int vehicleSegment = vehicleAI.GetSegmentVehicleIsIn();

            if(IsRedLightSegment(vehicleSegment)){
                vehicleAI.vehicleStatus = Status.STOP;
                vehiclesQueue.Add(_vehicle);
            }
            else{
                vehicleAI.vehicleStatus = Status.GO;
            }
        }

        void ExitLight(GameObject _vehicle){
            _vehicle.GetComponent<VehicleAI>().vehicleStatus = Status.GO;
        }

        bool IsRedLightSegment(int _vehicleSegment){
            if(currentRedLightsGroup == 1){
                foreach(Segment segment in lightsNbr1){
                    if(segment.id == _vehicleSegment)
                        return true;
                }
            }
            else{
                foreach(Segment segment in lightsNbr2){
                    if(segment.id == _vehicleSegment)
                        return true;
                }
            }
            return false;
        }

        void MoveVehiclesQueue(){
            //Move all vehicles in queue
            List<GameObject> nVehiclesQueue = new List<GameObject>(vehiclesQueue);
            foreach(GameObject vehicle in vehiclesQueue){
                int vehicleSegment = vehicle.GetComponent<VehicleAI>().GetSegmentVehicleIsIn();
                if(!IsRedLightSegment(vehicleSegment)){
                    vehicle.GetComponent<VehicleAI>().vehicleStatus = Status.GO;
                    nVehiclesQueue.Remove(vehicle);
                }
            }
            vehiclesQueue = nVehiclesQueue;
        }

        bool IsPrioritySegment(int _vehicleSegment){
            foreach(Segment s in prioritySegments){
                if(_vehicleSegment == s.id)
                    return true;
            }
            return false;
        }

        bool IsAlreadyInIntersection(GameObject _target){
            foreach(GameObject vehicle in vehiclesInIntersection){
                if(vehicle.GetInstanceID() == _target.GetInstanceID()) return true;
            }
            foreach(GameObject vehicle in vehiclesQueue){
                if(vehicle.GetInstanceID() == _target.GetInstanceID()) return true;
            }

            return false;
        }

        int SelectNextGreenGroup()
        {
            int group1Count = GetGroupVehicleCount(group1Sensors);
            int group2Count = GetGroupVehicleCount(group2Sensors);

            Debug.Log($"[Intersection {id}] ---- Decision Evaluation ----");
            Debug.Log($"Group1Count: {group1Count}, WaitingTime: {group1WaitingTime}");
            Debug.Log($"Group2Count: {group2Count}, WaitingTime: {group2WaitingTime}");

            if (group1Count == 0 && group2Count == 0)
            {
                Debug.Log("[Decision] Both groups empty → Round Robin applied.");
                return lastGreenGroup == 1 ? 2 : 1;
            }

            if (group1Count != group2Count)
            {
                int selected = group1Count < group2Count ? 1 : 2;
                Debug.Log($"[Decision] Least Density First → Group {selected}");
                return selected;
            }

            if (!Mathf.Approximately(group1WaitingTime, group2WaitingTime))
            {
                int selected = group1WaitingTime > group2WaitingTime ? 1 : 2;
                Debug.Log($"[Decision] Waiting Time Priority → Group {selected}");
                return selected;
            }

            Debug.Log("[Decision] Full Tie → Round Robin applied.");
            return lastGreenGroup == 1 ? 2 : 1;
        }

        void SetGreenGroup(int _greenGroup)
        {
            currentRedLightsGroup = _greenGroup == 1 ? 2 : 1;

            Debug.Log($"[Intersection {id}] GREEN → Group {_greenGroup}");
            Debug.Log($"[Intersection {id}] RED → Group {currentRedLightsGroup}");

            if (_greenGroup == 1)
                group1WaitingTime = 0f;
            else
                group2WaitingTime = 0f;
        }

        int GetGroupVehicleCount(List<LaneSensor> _sensors){
            int count = 0;
            if(_sensors == null)
                return count;

            foreach(LaneSensor sensor in _sensors){
                if(sensor == null)
                    continue;
                count += sensor.VehicleCount;
            }
            return count;
        }


        private List<GameObject> memVehiclesQueue = new List<GameObject>();
        private List<GameObject> memVehiclesInIntersection = new List<GameObject>();

        public void SaveIntersectionStatus(){
            memVehiclesQueue = vehiclesQueue;
            memVehiclesInIntersection = vehiclesInIntersection;
        }

        public void ResumeIntersectionStatus(){
            foreach(GameObject v in vehiclesInIntersection){
                foreach(GameObject v2 in memVehiclesInIntersection){
                    if(v.GetInstanceID() == v2.GetInstanceID()){
                        v.GetComponent<VehicleAI>().vehicleStatus = v2.GetComponent<VehicleAI>().vehicleStatus;
                        break;
                    }
                }
            }
            foreach(GameObject v in vehiclesQueue){
                foreach(GameObject v2 in memVehiclesQueue){
                    if(v.GetInstanceID() == v2.GetInstanceID()){
                        v.GetComponent<VehicleAI>().vehicleStatus = v2.GetComponent<VehicleAI>().vehicleStatus;
                        break;
                    }
                }
            }
        }
    }
}
