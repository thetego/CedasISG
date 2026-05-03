using UnityEngine;
using UnityEngine.SceneManagement;

public class Scene : MonoBehaviour
{
	public void SCE ( int id)
	{
		SceneManager.LoadScene(id);
	}
}
