using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class MoneyBag : MonoBehaviour {

    public int value = 50;
    public float lifespan = 5f;

    private Rigidbody rb;

    void Start(){
        gameObject.GetComponent<Rigidbody>().angularVelocity = Random.insideUnitSphere * 5;
    }

    void Update(){
        Lifecycle();
    }

    void Lifecycle(){
        lifespan -= Time.deltaTime;
        if (lifespan <= 0f){
            Destroy(gameObject);
        }
    }

    public void GiveMoney(){
        PlayerStats.cash += value;
        PlayerStats.PowerupsGained++;
    }

}
