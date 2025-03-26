using UnityEngine;
using NativeWebSocket;
using System.Threading.Tasks;
using UnityEngine.Networking;
using System.Collections;

public class GameController : MonoBehaviour
{
    private WebSocket ws;
    public float playerSpeed = 5f;
    public SpriteRenderer playerSpriteRenderer;

    private static GameController _instance;
    public static GameController Instance => _instance;

    private Material circleMaskMaterial;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            _instance = this;
            DontDestroyOnLoad(this.gameObject);
        }

        circleMaskMaterial = new Material(Shader.Find("Custom/CircleMask"));
        circleMaskMaterial.SetFloat("_Radius", 0.5f);
        circleMaskMaterial.SetFloat("_Softness", 0.05f);
    }

    async void Start()
    {
        ws = new WebSocket("ws://localhost:3000");

        ws.OnOpen += () =>
        {
            Debug.Log("Conexión WebSocket establecida");
            ws.SendText("get-speed");
        };

        ws.OnMessage += (bytes) =>
        {
            var message = System.Text.Encoding.UTF8.GetString(bytes);
            Debug.Log("Mensaje recibido: " + message);

            var data = JsonUtility.FromJson<WebSocketMessage>(message);
            if (data.type == "player-speed-updated")
            {
                playerSpeed = data.speed;
                Debug.Log("Nueva velocidad recibida: " + playerSpeed);
                UpdateAllPlayersSpeed(playerSpeed);
            }
            else if (data.type == "player-image-updated")
            {
                Debug.Log("Nueva imagen recibida: " + data.imageUrl);
                LoadPlayerImage(data.imageUrl);
            }
        };

        ws.OnClose += (e) =>
        {
            Debug.Log("Conexión WebSocket cerrada: " + e);
            Reconnect();
        };

        ws.OnError += (e) =>
        {
            Debug.LogError("Error en WebSocket: " + e);
        };

        await Connect();
    }

    async Task Connect()
    {
        try
        {
            await ws.Connect();
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error al conectar: " + e.Message);
        }
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (ws != null && ws.State == WebSocketState.Open)
        {
            ws.DispatchMessageQueue();
        }
#endif
    }

    private int reconnectAttempts = 0;
    private const int maxReconnectAttempts = 5;

    async void Reconnect()
    {
        if (reconnectAttempts >= maxReconnectAttempts)
        {
            Debug.LogError("Máximo de intentos de reconexión alcanzado");
            return;
        }

        reconnectAttempts++;
        Debug.Log($"Intentando reconectar... Intento {reconnectAttempts}");

        await Task.Delay(1000 * reconnectAttempts);
        await Connect();
    }

    private void UpdateAllPlayersSpeed(float newSpeed)
    {
        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        Debug.Log($"Encontrados {players.Length} jugadores para actualizar");
        
        foreach (PlayerController player in players)
        {
            if (player.IsServer)
            {
                player.UpdateSpeedClientRpc(newSpeed);
            }
            else
            {
                player.Speed = newSpeed;
            }
        }
    }

    private void LoadPlayerImage(string imageUrl)
    {
        StartCoroutine(DownloadImage(imageUrl));
    }

    private IEnumerator DownloadImage(string imageUrl)
{
    using (UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(imageUrl))
    {
        yield return webRequest.SendWebRequest();

        if (webRequest.result == UnityWebRequest.Result.Success)
        {
            Texture2D texture = DownloadHandlerTexture.GetContent(webRequest);
            
            float aspectRatio = (float)texture.width / texture.height;
            circleMaskMaterial.SetFloat("_AspectRatio", aspectRatio);
            
            Sprite sprite = Sprite.Create(
                texture, 
                new Rect(0, 0, texture.width, texture.height), 
                new Vector2(0.5f, 0.5f)
            );
            
            PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (PlayerController player in players)
            {
                SpriteRenderer renderer = player.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.sprite = sprite;
                    renderer.material = circleMaskMaterial;
                    
                    CircleCollider2D collider = player.GetComponent<CircleCollider2D>();
                    if (collider != null)
                    {
                        collider.radius = sprite.bounds.extents.magnitude * 0.5f;
                    }
                }
            }
        }
        else
        {
            Debug.LogError("Error al cargar imagen: " + webRequest.error);
        }
    }
}

    async void OnDestroy()
    {
        if (ws != null && ws.State == WebSocketState.Open)
        {
            await ws.Close();
        }

        if (circleMaskMaterial != null)
        {
            Destroy(circleMaskMaterial);
        }
    }

    [System.Serializable]
    private class WebSocketMessage
    {
        public string type;
        public float speed;
        public string imageUrl;
    }
}