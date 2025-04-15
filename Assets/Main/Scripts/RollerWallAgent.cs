using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class RollerWallAgent : Agent
{
	Rigidbody rBody;
	public Transform Target;
	public Transform Target2;
	public float forceMultiplier = 10f;
	public float jumpForce = 5f;

	private bool target1Reached;
	private bool target2Reached;
	private bool isGrounded;
	private bool firstEpisode = true;
	private bool resetOnNextEpisode = false;

	public override void Initialize()
	{
		rBody = GetComponent<Rigidbody>();
	}

	public override void OnEpisodeBegin()
	{
		if (transform.localPosition.y < 0 || resetOnNextEpisode)
		{
			rBody.linearVelocity = Vector3.zero;
			rBody.angularVelocity = Vector3.zero;
			transform.localPosition = new Vector3(0, 0.5f, -4);
		}

		isGrounded = true;
		target1Reached = false;
		target2Reached = false;

		Target.gameObject.SetActive(true);
		Target2.gameObject.SetActive(true);

		if (!firstEpisode)
		{
			Target.localPosition = GetSafeTargetPosition(topHalf: false);
			do
			{
				Target2.localPosition = GetSafeTargetPosition(topHalf: true);
			}
			while (Vector3.Distance(Target.localPosition, Target2.localPosition) < 4f);
		}

		firstEpisode = false;
		resetOnNextEpisode = false; // clear flag
	}

	Vector3 GetSafeTargetPosition(bool topHalf)
	{
		Vector3 pos;
		int tries = 0;
		do
		{
			float x = Random.Range(-4f, 4f);
			float z = topHalf
				? Random.Range(1.7f, 4.45f)
				: Random.Range(-4.45f, -1.7f);
			pos = new Vector3(x, 0.5f, z);
			tries++;
		} while (IsInsideWall(pos) && tries < 100);

		return pos;
	}

	bool IsInsideWall(Vector3 pos)
	{
		return pos.x >= -1.5f && pos.x <= 1.5f && pos.z >= -1f && pos.z <= 1f;
	}

	public override void CollectObservations(VectorSensor sensor)
	{
		sensor.AddObservation(Target.localPosition);
		sensor.AddObservation(Target2.localPosition);
		sensor.AddObservation(transform.localPosition);
		sensor.AddObservation(rBody.linearVelocity.x);
		sensor.AddObservation(rBody.linearVelocity.z);
		sensor.AddObservation(isGrounded ? 1f : 0f);
	}

	public override void OnActionReceived(ActionBuffers actionBuffers)
	{
		Vector3 controlSignal = Vector3.zero;
		controlSignal.x = actionBuffers.ContinuousActions[0];
		controlSignal.z = actionBuffers.ContinuousActions[1];
		rBody.AddForce(controlSignal * forceMultiplier);

		float jumpInput = actionBuffers.ContinuousActions.Length > 2 ? actionBuffers.ContinuousActions[2] : 0f;
		if (jumpInput > 0.5f && isGrounded)
		{
			rBody.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
			isGrounded = false;
		}

		float d1 = Vector3.Distance(transform.localPosition, Target.localPosition);
		float d2 = Vector3.Distance(transform.localPosition, Target2.localPosition);

		if (!target1Reached && d1 < 1.42f)
		{
			target1Reached = true;
			AddReward(0.25f);
			Target.gameObject.SetActive(false);
		}

		if (!target2Reached && d2 < 1.42f)
		{
			target2Reached = true;
			AddReward(0.25f);
			Target2.gameObject.SetActive(false);
		}

		if (target1Reached && target2Reached)
		{
			AddReward(0.5f); // base reward

			// Early completion bonus (scaled from 0 to 0.5)
			float bonus = Mathf.Clamp01((MaxStep - StepCount) / (float)MaxStep);
			AddReward(bonus * 0.5f);

			EndEpisode();
		}

		if (transform.localPosition.y < 0)
		{
			SetReward(-1.0f);
			EndEpisode();
		}

		// Small penalty to encourage efficiency
		AddReward(-0.001f);
	}

	private void OnCollisionEnter(Collision collision)
	{
		if (collision.gameObject.name == "Wall")
		{
			SetReward(-1.0f);
			resetOnNextEpisode = true;  // flag to reset position
			EndEpisode();
		}

		if (collision.gameObject.CompareTag("Ground") || collision.contacts[0].normal.y > 0.5f)
		{
			isGrounded = true;
		}
	}

	public override void Heuristic(in ActionBuffers actionsOut)
	{
		var c = actionsOut.ContinuousActions;
		c[0] = Input.GetAxis("Horizontal");
		c[1] = Input.GetAxis("Vertical");
		c[2] = Input.GetKey(KeyCode.Space) ? 1f : 0f;
	}
}
