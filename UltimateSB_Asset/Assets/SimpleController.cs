using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SimpleController : MonoBehaviour
{
    public Rigidbody rigidbody;
    public float acceleration;

    void Update()
    {
        var h = Input.GetAxisRaw("Horizontal");
        var v = Input.GetAxisRaw("Vertical");

        rigidbody.velocity += (Vector3.forward * v + Vector3.right * h) * acceleration;
    }
}
