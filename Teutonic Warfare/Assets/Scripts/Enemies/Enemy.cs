using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour {

    public float enemyspeed = 10f;

    public int enemyHealth = 100;
    public int enemyBounty = 10;

    private Transform target;
    private int wavepointIndex = 0;

    void Start(){
        getPoints();
    }

    void Update(){
        moveEnemy();
    }

    void getPoints(){
        target = Waypoints.Points[0];
    }

    void moveEnemy(){
        Vector3 direction = target.position - transform.position;
        transform.Translate(direction.normalized * enemyspeed * Time.deltaTime, Space.World);

        if(Vector3.Distance(transform.position, target.position) <= 0.5f){
            getNextPoint();
            return;
        }
    }

    public void takeDamage(int amount){
        enemyHealth -= amount;
        if(enemyHealth == 0){
            Die();
        }
    }

    void Die(){
        PlayerStats.cash += enemyBounty;
        PlayerStats.EnemiesKilled++;
        Destroy(gameObject);

        WaveSpawner.EnemiesAlive--;
    }

    void getNextPoint(){

        if(wavepointIndex >= Waypoints.Points.Length - 1){
            AttackVillage();
            return;
        }

        wavepointIndex++;
        target = Waypoints.Points[wavepointIndex];
    }

    void AttackVillage(){
        PlayerStats.lives--;
        Destroy(gameObject);
    }

}
