using Unity.Netcode;
using UnityEngine;

public class BulletController : NetworkBehaviour
{
    public GameObject owner;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject == owner)
        {
            return;
        }

        if (IsServer)
        {
            if (other.CompareTag("Player"))
            {
                NetworkObject playerNetworkObject = other.GetComponent<NetworkObject>();
                if (playerNetworkObject != null)
                {
                    playerNetworkObject.Despawn();
                    Destroy(other.gameObject);
                }

                DestroyBullet();
            }
            else if (other.CompareTag("Obstacle"))
            {
                DestroyBullet();
            }
        }
    }

    void DestroyBullet()
    {
        GetComponent<NetworkObject>().Despawn();
        Destroy(gameObject);
    }
}