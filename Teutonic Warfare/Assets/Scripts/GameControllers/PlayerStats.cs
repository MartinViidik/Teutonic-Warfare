using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerStats : MonoBehaviour {

    public static int BuildingsPurchased;
    public static int ArrowsFired;
    public static int EnemiesKilled;
    public static int CashEarned;
    public static int PowerupsGained;

    public static int cash;
    public int startCash = 500;

    public static int lives;
    public int startLives = 5;

    void Start(){
        cash = startCash;
        lives = startLives;
    }

}
