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

    void Update()
    {
        if (!IsOwner) return;

        float moveX = Input.GetAxis("Horizontal") * Speed * Time.deltaTime;
        float moveY = Input.GetAxis("Vertical") * Speed * Time.deltaTime;
        transform.Translate(new Vector3(moveX, moveY, 0));

        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 direction = (mousePosition - transform.position).normalized;
            RequestShootServerRpc(direction);
        }
    }

    [ServerRpc]
    void RequestShootServerRpc(Vector2 direction)
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