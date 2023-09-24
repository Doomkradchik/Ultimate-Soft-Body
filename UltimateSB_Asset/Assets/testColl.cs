using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class testColl : MonoBehaviour
{
    private void OnCollisionExit(Collision collision)
    {
        Debug.Log("EFEFEF");
    }

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log("AAAAAAAA");
    }
}

public class FF : MeshCollider
{

}
