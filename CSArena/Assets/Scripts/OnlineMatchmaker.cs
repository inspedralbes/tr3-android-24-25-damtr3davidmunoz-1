using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using WebSocketSharp;
using System.Net;
using System.Net.Sockets;
using UnityEngine.SceneManagement;

public class OnlineMatchmaker : MonoBehaviour
{
    [Header("UI Configuration")]
    [SerializeField] private UIDocument menuUI;
    
    [Header("Server Configuration")]
    [SerializeField] private string serverUrl = "ws://localhost:3000";
    
    private WebSocket webSocket;
    private NetworkManager netManager;
    private bool isConnecting = false;

    private void Awake()
    {
        // Verificar primero si menuUI está asignado
        if (menuUI == null)
        {
            Debug.LogError("UIDocument no asignado en el Inspector. Buscando automáticamente...");
            menuUI = FindObjectOfType<UIDocument>();
            
            if (menuUI == null)
            {
                Debug.LogError("No se encontró UIDocument en la escena. Asigna manualmente en el Inspector.");
                return;
            }
        }

        netManager = NetworkManager.Singleton;
        
        if (netManager == null)
        {
            Debug.LogError("NetworkManager no encontrado en la escena.");
            return;
        }

        // Configura el transporte por defecto
        var transport = netManager.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.SetConnectionData("0.0.0.0", 7777);
        }
        else
        {
            Debug.LogError("UnityTransport no encontrado en NetworkManager.");
        }
        
        SetupUI();
    }

    private void SetupUI()
    {
        if (menuUI == null || menuUI.rootVisualElement == null)
        {
            Debug.LogError("No se puede configurar UI - UIDocument o rootVisualElement es null");
            return;
        }

        var root = menuUI.rootVisualElement;
        
        try
        {
            // Botón Crear Partida
            var createButton = root.Q<Button>("createButton");
            if (createButton != null)
            {
                createButton.clicked += () => {
                    if (!isConnecting) {
                        var joinPanel = root.Q<VisualElement>("joinPanel");
                        if (joinPanel != null) joinPanel.AddToClassList("hidden");
                        CreateGame();
                    }
                };
            }
            else
            {
                Debug.LogError("Botón 'createButton' no encontrado en la UI");
            }
            
            // Botón Unirse a Partida
            var joinButton = root.Q<Button>("joinButton");
            if (joinButton != null)
            {
                joinButton.clicked += () => {
                    var joinPanel = root.Q<VisualElement>("joinPanel");
                    if (joinPanel != null) joinPanel.RemoveFromClassList("hidden");
                };
            }
            else
            {
                Debug.LogError("Botón 'joinButton' no encontrado en la UI");
            }
            
            // Confirmar Unión
            var confirmButton = root.Q<Button>("confirmJoinButton");
            if (confirmButton != null)
            {
                confirmButton.clicked += () => {
                    if (!isConnecting) {
                        var codeInput = root.Q<TextField>("codeInput");
                        if (codeInput != null && !string.IsNullOrEmpty(codeInput.value)) {
                            JoinGame(codeInput.value);
                        }
                        else
                        {
                            Debug.Log("Por favor ingresa un código válido");
                        }
                    }
                };
            }
            else
            {
                Debug.LogError("Botón 'confirmJoinButton' no encontrado en la UI");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error configurando UI: {e.Message}");
        }
    }

    private void CreateGame()
    {
        if (isConnecting) return;
        
        isConnecting = true;
        Debug.Log("Intentando crear partida...");
        
        try
        {
            webSocket = new WebSocket(serverUrl);
            
            webSocket.OnOpen += (sender, e) => {
                Debug.Log("Conexión WebSocket establecida (Host)");
                string ip = GetLocalIP();
                ushort port = netManager.GetComponent<UnityTransport>().ConnectionData.Port;
                
                string msg = $"{{\"type\":\"create-room\",\"hostIP\":\"{ip}\",\"port\":{port}}}";
                webSocket.Send(msg);
            };
            
            webSocket.OnMessage += (sender, e) => {
                Debug.Log($"Mensaje recibido: {e.Data}");
                var response = JsonUtility.FromJson<RoomResponse>(e.Data);
                
                if (response != null && response.success) {
                    ExecuteOnMainThread(() => {
                        // Muestra el código de sala
                        var root = menuUI.rootVisualElement;
                        var roomCodeLabel = root.Q<Label>("roomCodeLabel");
                        var roomCodePanel = root.Q<VisualElement>("roomCodePanel");
                        
                        if (roomCodeLabel != null) roomCodeLabel.text = response.roomCode;
                        if (roomCodePanel != null) roomCodePanel.RemoveFromClassList("hidden");
                        
                        // Inicia como host y cambia de escena
                        netManager.OnClientConnectedCallback += OnHostStarted;
                        if (!netManager.StartHost())
                        {
                            Debug.LogError("Falló al iniciar como host");
                            isConnecting = false;
                        }
                    });
                }
                else
                {
                    Debug.LogError("Respuesta del servidor inválida o fallida");
                    isConnecting = false;
                }
            };
            
            webSocket.OnError += (sender, e) => {
                Debug.LogError($"Error WebSocket: {e.Message}");
                isConnecting = false;
            };
            
            webSocket.OnClose += (sender, e) => {
                Debug.Log($"Conexión WebSocket cerrada: {e.Reason}");
                isConnecting = false;
            };
            
            webSocket.Connect();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error al crear partida: {e.Message}");
            isConnecting = false;
        }
    }

    private void JoinGame(string roomCode)
    {
        if (isConnecting) return;
        
        isConnecting = true;
        Debug.Log($"Intentando unirse a partida con código: {roomCode}");
        
        try
        {
            webSocket = new WebSocket(serverUrl);
            
            webSocket.OnOpen += (sender, e) => {
                Debug.Log("Conexión WebSocket establecida (Client)");
                string msg = $"{{\"type\":\"join-room\",\"roomCode\":\"{roomCode}\",\"playerIP\":\"{GetLocalIP()}\"}}";
                webSocket.Send(msg);
            };
            
            webSocket.OnMessage += (sender, e) => {
                Debug.Log($"Mensaje recibido: {e.Data}");
                var response = JsonUtility.FromJson<JoinResponse>(e.Data);
                
                if (response != null && response.success) {
                    ExecuteOnMainThread(() => {
                        // Configura la conexión al host
                        var transport = netManager.GetComponent<UnityTransport>();
                        transport.SetConnectionData(response.hostIP, (ushort)response.port);
                        
                        // Inicia como cliente
                        netManager.OnClientConnectedCallback += OnClientConnected;
                        if (!netManager.StartClient())
                        {
                            Debug.LogError("Falló al iniciar como cliente");
                            isConnecting = false;
                        }
                    });
                }
                else
                {
                    Debug.LogError("No se pudo unir a la partida: " + (response?.message ?? "Respuesta inválida"));
                    isConnecting = false;
                }
            };
            
            webSocket.OnError += (sender, e) => {
                Debug.LogError($"Error WebSocket: {e.Message}");
                isConnecting = false;
            };
            
            webSocket.OnClose += (sender, e) => {
                Debug.Log($"Conexión WebSocket cerrada: {e.Reason}");
                isConnecting = false;
            };
            
            webSocket.Connect();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error al unirse a partida: {e.Message}");
            isConnecting = false;
        }
    }

    private void OnHostStarted(ulong clientId)
    {
        if (clientId == netManager.LocalClientId) {
            Debug.Log("Host iniciado correctamente");
            netManager.OnClientConnectedCallback -= OnHostStarted;
            LoadGameScene();
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log("Cliente conectado al host");
        netManager.OnClientConnectedCallback -= OnClientConnected;
        LoadGameScene();
    }

    private void LoadGameScene()
    {
        if (menuUI != null && menuUI.rootVisualElement != null)
        {
            menuUI.rootVisualElement.visible = false;
        }
        
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.LoadScene("Game", LoadSceneMode.Single);
        }
        else
        {
            Debug.LogError("NetworkManager o SceneManager no disponible para cambiar de escena");
            SceneManager.LoadScene("Game");
        }
    }

    private string GetLocalIP()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList) {
                if (ip.AddressFamily == AddressFamily.InterNetwork) {
                    return ip.ToString();
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error obteniendo IP local: {e.Message}");
        }
        return "127.0.0.1";
    }

    private void ExecuteOnMainThread(System.Action action)
    {
        if (action == null) return;
        
        #if UNITY_WSA && !UNITY_EDITOR
        UnityEngine.WSA.Application.InvokeOnAppThread(() => action(), false);
        #else
        action();
        #endif
    }

    private void OnDestroy()
    {
        try
        {
            if (webSocket != null && webSocket.IsAlive)
            {
                webSocket.Close();
            }
            
            if (netManager != null) {
                netManager.OnClientConnectedCallback -= OnHostStarted;
                netManager.OnClientConnectedCallback -= OnClientConnected;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error en OnDestroy: {e.Message}");
        }
    }

    [System.Serializable]
    private class RoomResponse {
        public bool success;
        public string roomCode;
    }
    
    [System.Serializable]
    private class JoinResponse {
        public bool success;
        public string message;
        public string hostIP;
        public int port;
    }
}