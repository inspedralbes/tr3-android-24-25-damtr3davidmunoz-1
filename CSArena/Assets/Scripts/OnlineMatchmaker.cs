using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using WebSocketSharp;
using System.Net;
using System.Net.Sockets;
using UnityEngine.SceneManagement;

public class OnlineMatchmaker : NetworkBehaviour
{
    [Header("UI Configuration")]
    [SerializeField] private UIDocument menuUI;
    
    [Header("Server Configuration")]
    [SerializeField] private string serverUrl = "ws://localhost:3000";
    
    [Header("Network Configuration")]
    [SerializeField] private NetworkManager networkManagerPrefab;
    [SerializeField] private GameObject playerPrefab;
    
    private WebSocket webSocket;
    private bool isConnecting = false;
    private NetworkManager spawnedNetworkManager;
    private string currentRoomCode;

    private void Awake()
    {
        if (menuUI == null) menuUI = GetComponent<UIDocument>();
    }

    private void Start()
    {
        SetupUI();
    }

    private void SetupUI()
    {
        if (menuUI == null || menuUI.rootVisualElement == null) return;

        var root = menuUI.rootVisualElement;
        
        var createButton = root.Q<Button>("createButton");
        if (createButton != null)
        {
            createButton.clicked += () => {
                if (!isConnecting) CreateGame();
            };
        }
        
        var joinButton = root.Q<Button>("joinButton");
        if (joinButton != null)
        {
            joinButton.clicked += () => {
                root.Q<VisualElement>("joinPanel").RemoveFromClassList("hidden");
            };
        }
        
        var confirmJoinButton = root.Q<Button>("confirmJoinButton");
        if (confirmJoinButton != null)
        {
            confirmJoinButton.clicked += () => {
                string code = root.Q<TextField>("codeInput").value;
                if (!isConnecting && !string.IsNullOrEmpty(code)) JoinGame(code);
            };
        }
    }

    private void CreateGame()
    {
        if (isConnecting) return;
        
        isConnecting = true;
        Debug.Log("Iniciando creación de partida...");

        InitializeNetworkConnection();
        
        // Configurar el NetworkManager para manejar escenas
        NetworkManager.Singleton.SceneManager.SetClientSynchronizationMode(LoadSceneMode.Single);
        
        webSocket = new WebSocket(serverUrl);
        
        webSocket.OnOpen += (sender, e) => {
            Debug.Log("Conexión WebSocket establecida (Host)");
            
            MainThreadDispatcher.Run(() => {
                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                string ip = GetLocalIP();
                transport.SetConnectionData(ip, 7777);
                
                string msg = $"{{\"type\":\"create-room\",\"hostIP\":\"{ip}\",\"port\":7777}}";
                webSocket.Send(msg);
            });
        };
        
        webSocket.OnMessage += (sender, e) => {
            Debug.Log($"Mensaje recibido: {e.Data}");
            
            MainThreadDispatcher.Run(() => {
                var response = JsonUtility.FromJson<RoomResponse>(e.Data);
                if (response?.type == "room-created" && response.success)
                {
                    currentRoomCode = response.roomCode;
                    
                    var root = menuUI.rootVisualElement;
                    root.Q<Label>("roomCodeLabel").text = currentRoomCode;
                    root.Q<VisualElement>("roomCodePanel").RemoveFromClassList("hidden");
                    
                    // Registrar evento antes de iniciar host
                    NetworkManager.Singleton.OnServerStarted += OnHostStarted;
                    
                    if (!NetworkManager.Singleton.StartHost())
                    {
                        Debug.LogError("Error al iniciar host");
                        isConnecting = false;
                    }
                }
            });
        };
        
        webSocket.OnError += (sender, e) => {
            Debug.LogError($"Error WebSocket: {e.Message}");
            isConnecting = false;
        };
        
        webSocket.Connect();
    }

    private void JoinGame(string roomCode)
    {
        if (isConnecting) return;
        
        isConnecting = true;
        Debug.Log($"Uniéndose a partida: {roomCode}");

        InitializeNetworkConnection();
        
        // Configurar el NetworkManager para manejar escenas
        NetworkManager.Singleton.SceneManager.SetClientSynchronizationMode(LoadSceneMode.Single);
        
        webSocket = new WebSocket(serverUrl);
        
        webSocket.OnOpen += (sender, e) => {
            Debug.Log("Conexión WebSocket establecida (Client)");
            
            MainThreadDispatcher.Run(() => {
                string msg = $"{{\"type\":\"join-room\",\"roomCode\":\"{roomCode}\",\"playerIP\":\"{GetLocalIP()}\"}}";
                webSocket.Send(msg);
            });
        };
        
        webSocket.OnMessage += (sender, e) => {
            Debug.Log($"Mensaje recibido: {e.Data}");
            
            MainThreadDispatcher.Run(() => {
                var response = JsonUtility.FromJson<JoinResponse>(e.Data);
                if (response?.type == "join-response" && response.success)
                {
                    var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                    transport.SetConnectionData(response.hostIP, (ushort)response.port);
                    
                    // Registrar evento antes de iniciar cliente
                    NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                    
                    if (!NetworkManager.Singleton.StartClient())
                    {
                        Debug.LogError("Error al iniciar cliente");
                        isConnecting = false;
                    }
                }
                else
                {
                    Debug.LogError($"Error al unirse: {response?.message ?? "Respuesta inválida"}");
                    isConnecting = false;
                }
            });
        };
        
        webSocket.OnError += (sender, e) => {
            Debug.LogError($"Error WebSocket: {e.Message}");
            isConnecting = false;
        };
        
        webSocket.Connect();
    }

    private void InitializeNetworkConnection()
    {
        if (NetworkManager.Singleton == null && networkManagerPrefab != null)
        {
            spawnedNetworkManager = Instantiate(networkManagerPrefab);
            DontDestroyOnLoad(spawnedNetworkManager.gameObject);
        }
    }

    private void OnHostStarted()
    {
        Debug.Log("Host iniciado, cargando escena Game...");
        
        // Desregistrar el evento primero para evitar múltiples llamadas
        NetworkManager.Singleton.OnServerStarted -= OnHostStarted;
        
        // Cargar la escena Game para todos los clientes
        NetworkManager.Singleton.SceneManager.LoadScene("Game", LoadSceneMode.Single);
        
        // Ocultar la UI del menú
        if (menuUI != null && menuUI.rootVisualElement != null)
        {
            menuUI.rootVisualElement.visible = false;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("Cliente conectado, esperando cambio de escena...");
            
            // Desregistrar el evento
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            
            // Ocultar la UI del menú
            if (menuUI != null && menuUI.rootVisualElement != null)
            {
                menuUI.rootVisualElement.visible = false;
            }
        }
        
        // Solo el servidor instancia jugadores
        if (NetworkManager.Singleton.IsServer && playerPrefab != null)
        {
            var player = Instantiate(playerPrefab);
            player.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);
        }
    }

    private string GetLocalIP()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
        }
        catch { }
        return "127.0.0.1";
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        
        if (webSocket != null)
        {
            if (webSocket.IsAlive) webSocket.Close();
            webSocket = null;
        }
        
        if (spawnedNetworkManager != null)
        {
            Destroy(spawnedNetworkManager.gameObject);
        }
    }

    [System.Serializable]
    private class RoomResponse {
        public string type;
        public bool success;
        public string roomCode;
    }
    
    [System.Serializable]
    private class JoinResponse {
        public string type;
        public bool success;
        public string message;
        public string hostIP;
        public int port;
    }
}