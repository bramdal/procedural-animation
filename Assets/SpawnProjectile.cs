using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnProjectile : MonoBehaviour
{
    public GameObject projectile;
   
    // Update is called once per frame
    void Update()
    {
        if(Input.GetButtonDown("Fire1")){
            Instantiate(projectile);
        }
    }
}
