// Demo_Manager : Description : Use to know which sound is playing in the demo scene
// 2020.3.20f1

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TS.ZombieGames
{
	public class Demo_Manager : MonoBehaviour
	{
		public GameObject old_Sound;        // Refers to the last gameObject that play a sound
		public GameObject tmp_Play;     // Refers to the gameObject that play a sound

		public void PlayASound(string name_)
		{                   // -> Play a new sound
			if (old_Sound != null)
			{
				old_Sound.SendMessage("Demo_Button_Off");       // Stop the last sound
			}
			tmp_Play = GameObject.Find(name_);
			if (tmp_Play != old_Sound)
			{
				tmp_Play.SendMessage("Demo_Button_On");         // Play the new sound
				old_Sound = tmp_Play;
			}
			else
			{
				old_Sound = null;
			}
		}
	}
}