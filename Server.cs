//using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using System.Threading;

public class UdpServer : MonoBehaviour
{
    [SerializeField] private int port = 5066;
    [SerializeField] private string ipAddress = "127.0.0.1";
    
    private UdpClient udpClient;
    private IPEndPoint endPoint;

    //parent transforms for left and right hands
    public Transform rParent;
    public Transform lParent;

    // prefabs for individual landmark spheres
    public GameObject landmarkPrefab;
    public GameObject linePrefab;

    //scale multiplier applied to incoming world co-ords 
    public float multiplier = 10f;

    // might not be needed
    public int average = 1;
    const int LANDMARK_COUNT = 21;
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
        Pinky4 = 20
    }

    public class Hand
    {
        //buffer to accumulate landmark positions before averaging 
        public Vector3[] positionsBuffer = new Vector3[LANDMARK_COUNT];
        // scene instances for each landmark 
        public GameObject[] instances = new GameObject[LANDMARK_COUNT];

        // 5 line renderers (one per finger)
        public LineRenderer[] lines = new LineRenderer[5];
        
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

    public float sampleThreshold = 0.25f; // how many seconds of data should be averaged to produce a single pose of the hand.

    private void Start()
    {
        // Create a new UDP client
        udpClient = new UdpClient(port);

        // Set the endpoint to any IP address and port 0
        endPoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);

        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

        left = new Hand(lParent,landmarkPrefab,linePrefab);
        right = new Hand(rParent, landmarkPrefab,linePrefab);

        Thread t = new Thread(new ThreadStart(Run));
        t.Start();

    }
    private void Update()
    {
        UpdateHand(left);
        UpdateHand(right);
    }
    private void UpdateHand(Hand h) //if this continually shits itself try locking h
    {
        if (h.samplesCounter == 0) return;

        if (Time.timeSinceLevelLoad - h.lastSampleTime >= sampleThreshold)
        {
            for (int i = 0; i < LANDMARK_COUNT; ++i)
            {
                h.instances[i].transform.localPosition = h.positionsBuffer[i] / (float)h.samplesCounter * multiplier;
                h.positionsBuffer[i] = Vector3.zero;
            }

            h.reportedSamplesPerSecond = h.samplesCounter / (Time.timeSinceLevelLoad - h.lastSampleTime);
            h.lastSampleTime = Time.timeSinceLevelLoad;
            h.samplesCounter = 0f;

            h.UpdateLines();
        }
    }


    private void Run()
    {
        while (true)
        {
            try
            {
                
            
            // Check if there is any data available
            if (udpClient.Available > 0)
            {
                // Receive the data and endpoint of the sender
                byte[] data = udpClient.Receive(ref endPoint);

                // Convert the data to a string
                string str = Encoding.ASCII.GetString(data);

                // Print the message to the console
                Debug.Log("Received message: " + str.Substring(0, Mathf.Min(200, str.Length)));
                
                Hand h = null;

                    string[] lines = str.Split('\n'); // splits them by line so you just get e.g. 'Right|8|0.0698|-0.0152|0.0074'
                    foreach (string l in lines)
                    {
                        string[] s = l.Split('|');          // splits by the straight sl
                        if (s.Length < 5) continue;
                        int i;
                        if (s[0] == "Left") h = left;
                        else if (s[0] == "Right") h = right;
                        if (!int.TryParse(s[1], out i)) continue;

                        //again if shitting itself occurs you need to lock h for these two lines
                        h.positionsBuffer[i] += new Vector3(float.Parse(s[2]), float.Parse(s[3]), float.Parse(s[4]));
                        h.samplesCounter += 1f / LANDMARK_COUNT;
                    }
            }

            }
            catch(System.Exception e)
            {
                Debug.Log("UDP receive error:" + e.Message);
            }
        }
    }

    private void OnDestroy()
    {
        // Close the UDP client when the object is destroyed
        udpClient.Close();
    }
}
