using UnityEngine;
using UnityEngine.UI;
using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Collections.Concurrent;
using TMPro;

public class TCPImageStreamClient : MonoBehaviour
{
    public string address;
    public int port;

    public Texture2D tex = null;
    public RawImage rawImage;
    public RectTransform rectTrans;

    public bool stretch = false;

    [SerializeField]
    TextMeshProUGUI statusText;

    Thread m_NetworkThread;
    bool m_NetworkRunning;
    ConcurrentQueue<byte[]> dataQueue = new ConcurrentQueue<byte[]>();

    private void Awake()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int i = 0; i < arguments.Length; ++i)
        {
            if ((arguments[i].ToString().Equals("-a") || arguments[i].ToString().Equals("--address")) && i < arguments.Length - 2)
            {
                address = arguments[i + 1];
                continue;
            }

            if ((arguments[i].ToString().Equals("-p") || arguments[i].ToString().Equals("--port")) && i < arguments.Length - 2)
            {
                port = int.Parse(arguments[i + 1]);
                continue;
            }
        }

        statusText.enabled = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.Escape))
            Application.Quit();

        if (!m_NetworkRunning)
        {
            Debug.Log("Retrying TCP server connection...");
            statusText.enabled = true;
            statusText.text = "Retrying TCP server connection at " + address + ":" + port + "...";
            if (m_NetworkThread != null)
                m_NetworkThread.Join(100);
            m_NetworkThread = new Thread(NetworkThread);
            m_NetworkThread.Start();
            m_NetworkRunning = true;
        }
        else
        {
            statusText.enabled = false;
        }

        if (Input.GetKeyDown(KeyCode.F))
            stretch = !stretch;

        byte[] data;
        if (dataQueue.Count > 0 && dataQueue.TryDequeue(out data))
        {
            if (tex == null)
                tex = new Texture2D(1, 1);
            tex.LoadImage(data);
            //tex.LoadRawTextureData(data);
            tex.Apply();
            rawImage.texture = tex;

            int rectW, rectH;

            if (stretch)
            {
                rectW = Screen.width;
                rectH = Screen.height;
            }
            else
            {
                rectW = tex.width;
                rectH = tex.height;
            }

            rectTrans.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rectW);
            rectTrans.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rectH);
            rectTrans.ForceUpdateRectTransforms();
        }
    }

    private void OnEnable()
    {
        m_NetworkRunning = true;
        m_NetworkThread = new Thread(NetworkThread);
        m_NetworkThread.Start();
    }

    private void OnDisable()
    {
        m_NetworkRunning = false;
        if (m_NetworkThread != null)
        {
            if (!m_NetworkThread.Join(100))
            {
                m_NetworkThread.Abort();
            }
        }
    }

    private void NetworkThread()
    {
        TcpClient client = new TcpClient();
        try
        {
            client.Connect(new IPEndPoint(IPAddress.Parse(address), port));
        }
        catch
        {
            m_NetworkRunning = false;
            return;
        }

        using (var stream = client.GetStream())
        {
            BinaryReader reader = new BinaryReader(stream);
            try
            {
                while (m_NetworkRunning && client.Connected && stream.CanRead)
                {
                    int length = reader.ReadInt32();
                    byte[] data = reader.ReadBytes(length);
                    dataQueue.Enqueue(data);
                }
            }
            catch
            {
                m_NetworkRunning = false;
                return;
            }
        }
    }
}