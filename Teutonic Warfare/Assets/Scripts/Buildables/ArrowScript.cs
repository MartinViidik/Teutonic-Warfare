using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArrowScript : MonoBehaviour {

    public float speed;
    public int damage = 100;

    public Rigidbody rb;

    void Awake(){
        getComponents();
    }

    void Update(){
        moveArrow();
    }

    void moveArrow(){
        rb.AddForce(speed, 0, 0, ForceMode.Impulse);
    }

    void getComponents(){
        rb = gameObject.GetComponent<Rigidbody>();
    }

    public void OnTriggerEnter(Collider collider){
        if (collider.tag == "Enemy"){
            Damage(collider.transform);
            Debug.Log("Damaging enemy");
            Destroy(gameObject);
        }
    }

    void Damage(Transform Enemy){
        Enemy e = Enemy.GetComponent<Enemy>();
        if(e != null){
            e.takeDamage(damage);
        }

    }

}
