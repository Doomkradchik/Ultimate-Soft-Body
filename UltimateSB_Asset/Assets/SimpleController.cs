using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SimpleController : MonoBehaviour
{
    public Rigidbody rigidbody;
    public float acceleration;
    public float angularAcceleration;

    void Update()
    {
        var h = Input.GetAxisRaw("Horizontal");
        var v = Input.GetAxisRaw("Vertical");

        rigidbody.velocity += (Vector3.forward * v ) * acceleration;
        rigidbody.angularVelocity += Vector3.up * h * angularAcceleration * Time.fixedDeltaTime;
        //rigidbody.AddForce(Vector3.forward * v * acceleration);
    }
}
