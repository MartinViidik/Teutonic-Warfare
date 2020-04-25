using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Enemy : MonoBehaviour
{

    public float enemyspeed;
    public float enemystrength;

    public int enemyHealth = 100;
    public float fhealth = 100;

    public int enemyBounty = 10;

    public Transform target;
    private int wavepointIndex = 0;

    public Image healthbar;
    public Canvas EnemyCanvas;

    void Start()
    {
        getPoints();
    }

    void Update()
    {
        moveEnemy();
        hideHealthBar();
    }

    public void getPoints()
    {
        target = Waypoints.Points[0];
    }

    public void moveEnemy()
    {
        enemyspeed = 5f;
        Vector3 direction = target.position - transform.position;
        transform.Translate(direction.normalized * enemyspeed * Time.deltaTime, Space.World);

        if (Vector3.Distance(transform.position, target.position) <= 0.5f)
        {
            getNextPoint();
            return;
        }
    }

    public void OnTriggerEnter(Collider collider)
    {
        if (collider.tag == "Barricade")
        {
            Debug.Log("Enemy hit barricade");
            Destroy(collider.gameObject);
        }
        if (collider.tag == "Moat")
        {
            enemyspeed = enemyspeed / 2;
        }
    }

    public void takeDamage(int amount)
    {
        enemyHealth -= amount;
        healthbar.fillAmount = enemyHealth / fhealth;
        if (enemyHealth == 0)
        {
            Die();
        }
    }

    public void hideHealthBar()
    {
        if (enemyHealth == 100)
        {
            EnemyCanvas.enabled = false;
        }
        else
        {
            EnemyCanvas.enabled = true;
        }
    }

    void Die()
    {
        PlayerStats.cash += enemyBounty;
        PlayerStats.EnemiesKilled++;
        Destroy(gameObject);

        WaveSpawner.EnemiesAlive--;
    }

    void getNextPoint()
    {

        if (wavepointIndex >= Waypoints.Points.Length - 1)
        {
            AttackVillage();
            return;
        }

        wavepointIndex++;
        target = Waypoints.Points[wavepointIndex];
    }

    void AttackVillage()
    {
        PlayerStats.lives--;
        Destroy(gameObject);
    }

}
