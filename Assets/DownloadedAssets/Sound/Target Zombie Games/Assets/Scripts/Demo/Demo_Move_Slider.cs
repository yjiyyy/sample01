// Demo_Move_Slider : Description : Use to move the slider when demo scene start

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TS.ZombieGames
{
	public class Demo_Move_Slider : MonoBehaviour
	{

		public Animator anim;       // use to access animatorComponent

		void Start()
		{
			anim = GetComponent<Animator>();    // Access Animator Component
		}

		public void DesactiveAnimator()
		{           // use to deactivate the animator component
			anim.enabled = false;
		}
	}
}