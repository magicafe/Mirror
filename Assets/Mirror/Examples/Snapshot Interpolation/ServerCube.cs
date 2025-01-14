using System.Collections.Generic;
using UnityEngine;

namespace Mirror.Examples.SnapshotInterpolationDemo
{
    public class ServerCube : MonoBehaviour
    {
        [Header("Components")]
        public ClientCube client;

        [Header("Movement")]
        public float distance = 10;
        public float speed = 3;
        Vector3 start;

        [Header("Snapshot Interpolation")]
        public float sendInterval = 0.05f; // send every 50 ms
        float lastSendTime;

        [Header("Latency Simulation")]
        [Tooltip("Latency in seconds")]
        public float latency = 0.05f; // 50 ms
        [Tooltip("Latency jitter, randomly added to latency.")]
        [Range(0, 1)] public float jitter = 0.05f;
        [Tooltip("Packet loss in %")]
        [Range(0, 1)] public float loss = 0.1f;
        [Tooltip("Scramble % of unreliable messages, just like over the real network. Mirror unreliable is unordered.")]
        [Range(0, 1)] public float scramble = 0.1f;

        // random
        // UnityEngine.Random.value is [0, 1] with both upper and lower bounds inclusive
        // but we need the upper bound to be exclusive, so using System.Random instead.
        // => NextDouble() is NEVER < 0 so loss=0 never drops!
        // => NextDouble() is ALWAYS < 1 so loss=1 always drops!
        System.Random random = new System.Random();

        // hold on to snapshots for a little while before delivering
        // <deliveryTime, snapshot>
        List<(double, Snapshot3D)> queue = new List<(double, Snapshot3D)>();

        // latency simulation:
        // always a fixed value + some jitter.
        float SimulateLatency() => latency + Random.value * jitter;

        void Start()
        {
            start = transform.position;
        }

        void Update()
        {
            // move on XY plane
            float x = Mathf.PingPong(Time.time * speed, distance);
            transform.position = new Vector3(start.x + x, start.y, start.z);

            // broadcast snapshots every interval
            if (Time.time >= lastSendTime + sendInterval)
            {
                Send(transform.position);
                lastSendTime = Time.time;
            }

            Flush();
        }

        void Send(Vector3 position)
        {
            // create snapshot
            Snapshot3D snap = new Snapshot3D(Time.timeAsDouble, 0, position);

            // simulate packet loss
            bool drop = random.NextDouble() < loss;
            if (!drop)
            {
                // simulate scramble (Random.Next is < max, so +1)
                bool doScramble = random.NextDouble() < scramble;
                int last = queue.Count;
                int index = doScramble ? random.Next(0, last + 1) : last;

                // simulate latency
                float simulatedLatency = SimulateLatency();
                double deliveryTime = Time.timeAsDouble + simulatedLatency;
                queue.Insert(index, (deliveryTime, snap));
            }
        }

        void Flush()
        {
            // flush ready snapshots to client
            for (int i = 0; i < queue.Count; ++i)
            {
                (double deliveryTime, Snapshot3D snap) = queue[i];
                if (Time.timeAsDouble >= deliveryTime)
                {
                    client.OnMessage(snap);
                    queue.RemoveAt(i);
                    --i;
                }
            }
        }
    }
}
