using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProjectilBehaiour : MonoBehaviour
{
    public float forceValue = 5f;

    private void Start() {
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.AddForce((GameObject.FindWithTag("Player").transform.position - transform.position) + Vector3.up * forceValue, ForceMode.Impulse);

        Destroy(gameObject, 3f);
    }
}
