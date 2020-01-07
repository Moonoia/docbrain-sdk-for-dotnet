namespace docBrain
{
    public class InferenceResult
    {
        public InferenceResult(string inResult, float inScore)
        {
            Result = inResult;
            Score = inScore;
        }

        public string Result { get; private set; }
        public float Score { get; private set; }
    }
}
