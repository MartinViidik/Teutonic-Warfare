using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class WaveSpawner : MonoBehaviour {

    public static int EnemiesAlive = 0;

    public Transform enemyPrefab;
    public Transform spawnPoint;
    public float waveCooldown = 5f;

    public float waveTime = 100f;
    public Text waveTimerText;

    private float timer = 2f;
    private int waveIndex = 0;

    void Update(){
        countTime();
    }

    void countTime(){
        if(!GameController.buyingMode){
            waveTime -= Time.deltaTime;
            waveTimerText.text = "" + Mathf.Round(waveTime);
            timer -= Time.deltaTime;
            if (timer <= 0f){
                StartCoroutine(spawnWave());
                timer = waveCooldown;
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

    void spawnEnemy(){
        Instantiate(enemyPrefab, spawnPoint.position, spawnPoint.rotation);
        EnemiesAlive++;
    }

}
