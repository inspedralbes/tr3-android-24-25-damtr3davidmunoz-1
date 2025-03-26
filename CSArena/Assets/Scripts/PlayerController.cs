using Unity.Netcode;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    private float _speed = 5f;
    public float Speed 
    {
        get => _speed;
        set 
        {
            _speed = value;
            Debug.Log($"Velocidad actualizada a: {_speed}");
        }
    }
    
    public GameObject bulletPrefab;
    public float bulletSpeed = 10f;
    private CircleCollider2D playerCollider;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            InitializePlayer();
        }
    }

    private void InitializePlayer()
    {
        playerCollider = GetComponent<CircleCollider2D>();
        AdjustColliderToSprite();
    }

    private void AdjustColliderToSprite()
    {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null && playerCollider != null)
        {
            // Usamos el menor entre el ancho y alto para hacer un círculo perfecto
            float spriteSize = Mathf.Min(renderer.bounds.size.x, renderer.bounds.size.y);
            playerCollider.radius = spriteSize * 0.5f; // Radio es la mitad del diámetro
            
            // Asegurar que el collider esté centrado
            playerCollider.offset = Vector2.zero;
        }
    }

    void Update()
    {
        if (!IsOwner) return;

        HandleMovement();
        HandleShooting();
    }

    private void HandleMovement()
    {
        float moveX = Input.GetAxis("Horizontal") * Speed * Time.deltaTime;
        float moveY = Input.GetAxis("Vertical") * Speed * Time.deltaTime;
        transform.Translate(new Vector3(moveX, moveY, 0));
    }

    private void HandleShooting()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 direction = (mousePosition - transform.position).normalized;
            RequestShootServerRpc(direction);
        }
    }

    [ServerRpc]
    private void RequestShootServerRpc(Vector2 direction)
    {
        Shoot(direction);
    }

    void Shoot(Vector2 direction)
    {
        GameObject bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
        Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
        bulletRb.linearVelocity = direction * bulletSpeed;

        bullet.GetComponent<BulletController>().owner = gameObject;
        bullet.GetComponent<NetworkObject>().Spawn();
        
        Destroy(bullet, 3f);
    }

    [ClientRpc]
    public void UpdateSpeedClientRpc(float newSpeed)
    {
        Speed = newSpeed;
    }
}