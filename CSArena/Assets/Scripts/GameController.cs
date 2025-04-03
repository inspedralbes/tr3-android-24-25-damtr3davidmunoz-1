using UnityEngine;
using NativeWebSocket;
using System.Threading.Tasks;
using UnityEngine.Networking;
using System.Collections;

public class GameController : MonoBehaviour
{
    private WebSocket ws;
    public float playerSpeed = 5f;
    public Sprite defaultPlayerSprite;
    
    private static GameController _instance;
    public static GameController Instance => _instance;

    private Material circleMaskMaterial;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject);

        circleMaskMaterial = new Material(Shader.Find("Custom/CircleMask"));
        circleMaskMaterial.SetFloat("_Radius", 0.5f);
        circleMaskMaterial.SetFloat("_Softness", 0.05f);
    }

    async void Start()
    {
        await LoadLastPlayerImage();
        
        await InitializeWebSocket();
    }

    private async Task LoadLastPlayerImage()
    {
        string url = "http://localhost:3000/api/last-player-image";
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            await request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<ImageResponse>(request.downloadHandler.text);
                if (response.success && !string.IsNullOrEmpty(response.imageUrl))
                {
                    await DownloadAndApplyImage(response.imageUrl);
                }
                else
                {
                    ApplyDefaultSprite();
                }
            }
            else
            {
                Debug.LogError("Error loading last image: " + request.error);
                ApplyDefaultSprite();
            }
        }
    }

    private async Task DownloadAndApplyImage(string imageUrl)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            await request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                
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
                Debug.LogError("Error downloading image: " + request.error);
                ApplyDefaultSprite();
            }
        }
    }

    private void ApplyDefaultSprite()
    {
        if (defaultPlayerSprite != null)
        {
            PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (PlayerController player in players)
            {
                SpriteRenderer renderer = player.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.sprite = defaultPlayerSprite;
                    renderer.material = circleMaskMaterial;
                }
            }
        }
    }

    private async Task InitializeWebSocket()
    {
        ws = new WebSocket("ws://localhost:3000");

        ws.OnOpen += () => 
        {
            Debug.Log("WebSocket connected");
            ws.SendText("get-speed");
        };

        ws.OnMessage += (bytes) =>
        {
            var message = System.Text.Encoding.UTF8.GetString(bytes);
            HandleWebSocketMessage(message);
        };

        ws.OnClose += (e) => 
        {
            Debug.Log("WebSocket closed: " + e);
            Reconnect();
        };

        ws.OnError += (e) => 
        {
            Debug.LogError("WebSocket error: " + e);
        };

        await ConnectWebSocket();
    }

    private void HandleWebSocketMessage(string message)
    {
        try
        {
            var data = JsonUtility.FromJson<WebSocketMessage>(message);
            
            switch (data.type)
            {
                case "player-speed-updated":
                    playerSpeed = data.speed;
                    UpdateAllPlayersSpeed(playerSpeed);
                    break;
                    
                case "player-image-updated":
                    _ = DownloadAndApplyImageAsync(data.imageUrl);
                    break;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error processing message: " + e.Message);
        }
    }

    private async Task DownloadAndApplyImageAsync(string imageUrl)
    {
        try
        {
            await DownloadAndApplyImage(imageUrl);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error downloading image: " + ex.Message);
            ApplyDefaultSprite();
        }
    }

    private void UpdateAllPlayersSpeed(float newSpeed)
    {
        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var player in players)
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

    private async Task ConnectWebSocket()
    {
        try
        {
            await ws.Connect();
        }
        catch (System.Exception e)
        {
            Debug.LogError("Connection error: " + e.Message);
        }
    }

    private async void Reconnect()
    {
        await Task.Delay(5000);
        if (ws != null && ws.State != WebSocketState.Open)
        {
            await ws.Connect();
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

    async void OnDestroy()
    {
        if (ws != null && ws.State == WebSocketState.Open)
        {
            await ws.Close();
        }
    }

    [System.Serializable]
    private class WebSocketMessage
    {
        public string type;
        public float speed;
        public string imageUrl;
    }

    [System.Serializable]
    private class ImageResponse
    {
        public bool success;
        public string imageUrl;
    }
}