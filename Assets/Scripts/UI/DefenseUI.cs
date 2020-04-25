using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class DefenseUI : MonoBehaviour {

    public Text livesText;
    public Text CashText;
    public GameObject gameoverUI;

    void Update(){
        displayStats();
        GetGameStatus();
    }

    public void PauseGame(){
        if (Time.timeScale == 1.0F){
            Time.timeScale = 0.0F;
        } else {
            Time.timeScale = 1.0F;
        }
    }

    public void displayStats(){
        CashText.text = PlayerStats.cash.ToString();
        livesText.text = PlayerStats.lives.ToString();
    }

    public void GetGameStatus(){
        if (GameController.gameEnded){
            endGameUI();
        }
    }

    public void endGameUI(){
        gameoverUI.SetActive(true);
    }

    public void Retry(){
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void Menu(){
        Debug.Log("Go to menu");
    }

}
