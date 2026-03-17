//using System.Diagnostics;
using System;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using System.Threading;
using System.Numerics;
using Vector3 = UnityEngine.Vector3;

public class UdpServer : MonoBehaviour
{
    //setting up udp server
    [SerializeField] private int port = 5066;
    [SerializeField] private string ipAddress = "127.0.0.1";

    private UdpClient udpClient;
    private IPEndPoint endPoint;

    private Thread receiveThread;
    private volatile bool isRunning = true;

    //parent transforms for left and right hands
    public Transform rParent;
    public Transform lParent;

    // prefabs for individual landmark spheres
    public GameObject landmarkPrefab;
    public GameObject linePrefab;

    // 
    
    //

    //scale multiplier applied to incoming world co-ords 
    public float multiplier = 10f;

    // might not be needed
    public int average = 1;
    const int LANDMARK_COUNT = 23;
    public enum Landmark { 
        Wrist = 0,
        Thumb1 = 1,
        Thumb2 = 2,
        Thumb3 = 3,
        Thumb4 = 4,
        Index1 = 5,
        Index2 = 6,
        Index3 = 7,
        Index4 = 8,
        Middle1 = 9,
        Middle2 = 10,
        Middle3 = 11,
        Middle4 = 12,
        Ring1 = 13,
        Ring2 = 14,
        Ring3 = 15,
        Ring4 = 16,
        Pinky1 = 17,
        Pinky2 = 18,
        Pinky3 = 19,
        Pinky4 = 20, 
        Elbow =21, 
        Shoulder = 22
  
    }

    public class Hand
    {
        //buffer to accumulate landmark positions before averaging 
        public Vector3[] positionsBuffer = new Vector3[LANDMARK_COUNT];

        // scene instances for each landmark 
        public GameObject[] instances = new GameObject[LANDMARK_COUNT];

        // 6 line renderers (one per finger + arm)
        public LineRenderer[] lines = new LineRenderer[6];

        //
        public Vector3[] writeBuffer = new Vector3[LANDMARK_COUNT];
        public Vector3[] readBuffer = new Vector3[LANDMARK_COUNT]; 
        public volatile bool newFrameAvailable = false; 

        
        // for debugging 
        public float reportedSamplesPerSecond;
        public float lastSampleTime;
        public float samplesCounter;

        public Hand(Transform parent, GameObject landmarkPrefab, GameObject linePrefab)
        {
            //create 21 landmark spheres 
            for (int i = 0; i < instances.Length; ++i)
            {
                instances[i] = Instantiate(landmarkPrefab);// GameObject.CreatePrimitive(PrimitiveType.Sphere);
                instances[i].transform.localScale = Vector3.one * 0.1f;
                instances[i].transform.parent = parent;
            }

            // create 5 finger line renderers 
            for (int i = 0; i < lines.Length; ++i)
            {
                lines[i] = Instantiate(linePrefab).GetComponent<LineRenderer>();
            }
        }

        public void UpdateLines()
        {
            for (int i = 0; i < 5; i++)
                lines[i].positionCount = 5;

            lines[0].SetPosition(0, instances[(int)Landmark.Wrist].transform.position);
            lines[0].SetPosition(1, instances[(int)Landmark.Thumb1].transform.position);
            lines[0].SetPosition(2, instances[(int)Landmark.Thumb2].transform.position);
            lines[0].SetPosition(3, instances[(int)Landmark.Thumb3].transform.position);
            lines[0].SetPosition(4, instances[(int)Landmark.Thumb4].transform.position);

            lines[1].SetPosition(0, instances[(int)Landmark.Wrist].transform.position);
            lines[1].SetPosition(1, instances[(int)Landmark.Index1].transform.position);
            lines[1].SetPosition(2, instances[(int)Landmark.Index2].transform.position);
            lines[1].SetPosition(3, instances[(int)Landmark.Index3].transform.position);
            lines[1].SetPosition(4, instances[(int)Landmark.Index4].transform.position);

            lines[2].SetPosition(0, instances[(int)Landmark.Wrist].transform.position);
            lines[2].SetPosition(1, instances[(int)Landmark.Middle1].transform.position);
            lines[2].SetPosition(2, instances[(int)Landmark.Middle2].transform.position);
            lines[2].SetPosition(3, instances[(int)Landmark.Middle3].transform.position);
            lines[2].SetPosition(4, instances[(int)Landmark.Middle4].transform.position);

            lines[3].SetPosition(0, instances[(int)Landmark.Wrist].transform.position);
            lines[3].SetPosition(1, instances[(int)Landmark.Ring1].transform.position);
            lines[3].SetPosition(2, instances[(int)Landmark.Ring2].transform.position);
            lines[3].SetPosition(3, instances[(int)Landmark.Ring3].transform.position);
            lines[3].SetPosition(4, instances[(int)Landmark.Ring4].transform.position);

            lines[4].SetPosition(0, instances[(int)Landmark.Wrist].transform.position);
            lines[4].SetPosition(1, instances[(int)Landmark.Pinky1].transform.position);
            lines[4].SetPosition(2, instances[(int)Landmark.Pinky2].transform.position);
            lines[4].SetPosition(3, instances[(int)Landmark.Pinky3].transform.position);
            lines[4].SetPosition(4, instances[(int)Landmark.Pinky4].transform.position);

            // Arm: Wrist -> Elbow -> Shoulder
            lines[5].positionCount = 3;
            lines[5].SetPositions(new Vector3[] {
                instances[(int)Landmark.Wrist].transform.position,
                instances[(int)Landmark.Elbow].transform.position,
                instances[(int)Landmark.Shoulder].transform.position
            });

        }
        public float GetFingerAngle(Landmark referenceFrom, Landmark referenceTo, Landmark from, Landmark to)
        {
            Vector3 reference = (instances[(int)referenceTo].transform.position - instances[(int)referenceFrom].transform.position).normalized;
            Vector3 direction = (instances[(int)to].transform.position - instances[(int)from].transform.position).normalized;
            return Vector3.SignedAngle(reference, direction, Vector3.Cross(reference, direction));
        }

    }

    private Hand left;
    private Hand right;

    //public float sampleThreshold = 0.25f; // how many seconds of data should be averaged to produce a single pose of the hand.

    private void Start()
    {
        // Create a new UDP client
        udpClient = new UdpClient(port);

        // Set the endpoint to any IP address and port 0
        endPoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);

        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

        left = new Hand(lParent,landmarkPrefab,linePrefab);
        right = new Hand(rParent, landmarkPrefab,linePrefab);

        //Thread t = new Thread(new ThreadStart(Run));
        //t.Start();
        receiveThread = new Thread(Run);
        receiveThread.IsBackground = true;
        receiveThread.Start();

    }
private void Update()
{
    if (left.newFrameAvailable)
        UpdateHand(left);

    if (right.newFrameAvailable)
        UpdateHand(right);
}

//     private void Update()
// {
//     if (!left.newFrameAvailable && !right.newFrameAvailable)
//         return;

//     if (Time.time < nextProcessTime)
//         return;

//     nextProcessTime = Time.time + (1f / targetProcessingFPS);

//     UpdateHand(left);
//     UpdateHand(right);
// }

//fafo start 
    private void UpdateHand(Hand h) //if this continually shits itself try locking h
    {
        //if (h.samplesCounter == 0) return;
        if(!h.newFrameAvailable) 
            return; 

        for (int i = 0; i < LANDMARK_COUNT; i++)
        {
            h.readBuffer[i] = h.writeBuffer[i];
        }

        // var temp = h.readBuffer;
        // h.readBuffer = h.writeBuffer;
        // h.writeBuffer = temp;
        //Debug.Log(h.readBuffer);
        //HERE
        

        h.newFrameAvailable = false;
        float smoothing = 0.5f;

        //if (Time.timeSinceLevelLoad - h.lastSampleTime >= sampleThreshold)
        {
            for (int i = 0; i < LANDMARK_COUNT; ++i)
            {   
                //test 2
                //h.instances[i].transform.localPosition = h.readBuffer[i];

                //test 1
                //h.instances[i].transform.localPosition = h.positionsBuffer[i] / (float)h.samplesCounter * multiplier;
                //h.positionsBuffer[i] = Vector3.zero;

                //test 3 
                // float smoothSpeed = 50f;

                // h.instances[i].transform.localPosition =
                //     Vector3.Lerp(
                //         h.instances[i].transform.localPosition,
                //         h.readBuffer[i],
                //         Time.deltaTime * smoothSpeed
                //     );

                //test 4 
                    //h.readBuffer[i] = h.writeBuffer[i]; 

                //test 5 THIS WORKS FOR HANDS - and i think the one ishould be using
                // float smoothing = 0.3f; 

                // h.instances[i].transform.localPosition =
                //     Vector3.Lerp(
                //         h.instances[i].transform.localPosition,
                //         h.readBuffer[i],
                //         smoothing
                //     );

                //test 6 - i want to just straight up plot to help show me whats going onin python
                // i think this will fix the blinking 
                Vector3 targetPos = h.readBuffer[i];
                if (targetPos != Vector3.zero)
                {
                    //h.instances[i].transform.localPosition = newPos; 
                    h.instances[i].transform.localPosition =
                Vector3.Lerp(h.instances[i].transform.localPosition, targetPos, smoothing);
                }
            }

            //h.reportedSamplesPerSecond = h.samplesCounter / (Time.timeSinceLevelLoad - h.lastSampleTime);
            //h.lastSampleTime = Time.timeSinceLevelLoad;
            //h.samplesCounter = 0f;

            h.UpdateLines();
        }
    }
    /////fafo end 
    /// 


    private void Run()
{
    while (isRunning)
    {
        try
        {
            byte[] data = udpClient.Receive(ref endPoint);

            Debug.Log($"Packet size: {data.Length}");

            int offset = 0;

            // Each hand = 1 byte + 23 * 3 floats - this used to be 21 with 21 landmarks (added 2 elbows and shoudlers)
            const int HAND_SIZE = 1 + 23 * 3 * 4; // 1 byte + 23*3 floats * 4 bytes per float

            while (offset + HAND_SIZE <= data.Length)
            {
                byte handId = data[offset];
                offset += 1;

                Hand h = (handId == 0) ? left : right;

                    //attempt 1 million and 1
                    for (int i = 0; i < LANDMARK_COUNT; i++)
                    {
                        float x = BitConverter.ToSingle(data, offset); offset += 4;
                        float y = BitConverter.ToSingle(data, offset); offset += 4;
                        float z = BitConverter.ToSingle(data, offset); offset += 4;

                        // Just multiply by a fixed multiplier to scale world units to Unity
                        h.writeBuffer[i] = new Vector3(x, -y, z) * multiplier;
                    }
  
                    



                    //// DEBUG: print received frame
                    // StringBuilder sb = new StringBuilder();
                    // sb.Append(handId == 0 ? "LEFT HAND\n" : "RIGHT HAND\n");

                    // for (int i = 0; i < LANDMARK_COUNT; i++)
                    // {
                    //     Vector3 v = h.writeBuffer[i];
                    //     sb.AppendLine($"Landmark {i}: {v}");
                    // }

                    // Debug.Log(sb.ToString());
                    ////
                    
                    // Vector3 elbow = h.writeBuffer[21];
                    // Vector3 shoulder = h.writeBuffer[22];

                    // Debug.Log($"Elbow: {elbow}  Shoulder: {shoulder}");

                    h.newFrameAvailable = true; 
                
            }
        }
        catch (SocketException)
        {       
            if (!isRunning)
                return;
        }
        catch (ObjectDisposedException)
        {
            // Happens when the socket is closed while Receive() is blocking
            if (!isRunning)
                return;
        }
        catch (Exception e)
        {
            Debug.Log("UDP receive error: " + e.Message);
        }
    }
}

    private void OnDestroy()
    {
            // Close the UDP client when the object is destroyed
        isRunning = false; 

        if (udpClient != null)
            udpClient.Close();

        if (receiveThread != null)
            receiveThread.Join();


    }
}
