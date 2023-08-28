using UnityEngine;
using Random = UnityEngine.Random;

public class BallThrower : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private float _cooldown;
    [SerializeField] private Rigidbody _prefab;
    [SerializeField] private float _force;
    private float _requiredTime = 0f;

    public bool rotate = true;
    public float rotVelocity;

    private void Update()
    {
        if (Input.GetMouseButton(0))
            TryShoot();
    }

    private void TryShoot()
    {
       if(Time.time > _requiredTime)
       {
            var ray = _camera.ScreenPointToRay(_camera.ViewportToScreenPoint(Vector2.one * 0.5f));
            var ball = Instantiate(_prefab, ray.origin, Quaternion.identity);
            ball.AddForce(_force * ray.direction, ForceMode.Impulse);
            if(rotate) ball.angularVelocity = Random.insideUnitSphere * rotVelocity;
            _requiredTime = Time.time + _cooldown;
       }   
    }
}
