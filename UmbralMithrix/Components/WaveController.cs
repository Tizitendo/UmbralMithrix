using UnityEngine;

namespace UmbralMithrix;

[RequireComponent(typeof(CharacterController))]
public class WaveController : MonoBehaviour
{
    private CharacterController _characterController;
    void Start()
    {
        _characterController = GetComponent<CharacterController>();
    }

    private void FixedUpdate()
    {
        if (_characterController.velocity.magnitude <= 10)
        {
            Destroy(gameObject);
        }
    }
}