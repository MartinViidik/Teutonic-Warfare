using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class WaveSpawner : MonoBehaviour {

    public static int EnemiesAlive = 0;

    public Transform enemyPrefab;
    public Transform powerupPrefab;

    public Transform spawnPoint;
    public float waveCooldown = 5f;
    public float powerupCooldown = 10f;

    public float waveTime = 100f;
    public Text waveTimerText;

    private float enemytimer = 2f;
    private float poweruptimer;
    private int waveIndex = 0;

    public Vector3 center;
    public Vector3 size;

    void Update(){
        countTime();
    }

    void countTime(){
        if(!GameController.buyingMode){
            waveTime -= Time.deltaTime;
            waveTimerText.text = "" + Mathf.Round(waveTime);
            if (waveTime <= 0f){
                GameController.levelWon = true;
                GameController.gameEnded = true;
            }

            enemytimer -= Time.deltaTime;
            if (enemytimer <= 0f){
                StartCoroutine(spawnWave());
                enemytimer = waveCooldown;
            }

            poweruptimer = Random.Range(20, 40);
            poweruptimer -= Time.deltaTime;
            if(poweruptimer <= 0f){
                StartCoroutine(spawnPowerup());
                poweruptimer = powerupCooldown;
            }
        }
    }

    IEnumerator spawnWave(){
        waveIndex++;
        Debug.Log("Spawning wave: " + waveIndex);
        for (int i = 0; i < waveIndex; i++){
            spawnEnemy();
            yield return new WaitForSeconds(0.5f);
        }
    }

    IEnumerator spawnPowerup(){
        for (int i = 0; i < waveIndex; i++){
            placePowerup();
            yield return new WaitForSeconds(0.5f);
        }
    }

    void spawnEnemy(){
        Instantiate(enemyPrefab, spawnPoint.position, spawnPoint.rotation);
        EnemiesAlive++;
    }

    void placePowerup(){
        Vector3 SpawnPosition = center + new Vector3(Random.Range(-size.x / 2, size.x / 2), Random.Range(-size.y / 2, size.y / 2), Random.Range(-size.z / 2, size.z /2));
        Instantiate(powerupPrefab, SpawnPosition, Quaternion.identity);
        Debug.Log("Spawning powerup");
    }

    void OnDrawGizmosSelected(){
        Gizmos.color = new Color(1, 0, 0, 0.5f);
        Gizmos.DrawCube(center, size);
    }

}
