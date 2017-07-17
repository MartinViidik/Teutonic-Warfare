using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArcherController : MonoBehaviour {

    public float fireRate = 1f;
    private float fireCountdown;

    public GameObject arrowPrefab;
    public Transform firePoint;

	void Update () {
        fireCounter();
	}

    void fireCounter(){
        if(!GameController.buyingMode){
            fireCountdown -= Time.deltaTime;
            if (fireCountdown <= 0f){
                shootBow();
                PlayerStats.ArrowsFired++;
                fireCountdown = 1f / fireRate;
            }
        }
    }

    void shootBow(){
        Debug.Log("Shooting");
        GameObject arrowFlying = Instantiate(arrowPrefab, firePoint.position, firePoint.localRotation);
        ArrowScript arrow = arrowFlying.GetComponent<ArrowScript>();
        Destroy(arrowFlying, 4f);
    }
}
