import unittest
from score import measure,model_scores

class AgreementMetrics(unittest.TestCase):
    def test_swapping_the_operations_can_keep_both_axis_sets(self):
        one={"x":"mixed/write"}
        a={"x":["source/read","docs/write"]}
        b={"x":["docs/read","source/write"]}
        s=measure(["x"],one,one,a,b)
        self.assertEqual(s["mean_jaccard"],0)
        self.assertEqual(s["multi_target_exact"],1)
        self.assertEqual(s["multi_action_exact"],1)
        self.assertEqual(s["collapse_hidden_diff"],1)

    def test_a_missing_effect_gets_partial_credit(self):
        one={"x":"mixed/write"}
        a={"x":["source/read","docs/write"]}
        b={"x":["source/read"]}
        s=measure(["x"],one,one,a,b)
        self.assertEqual(s["mean_jaccard"],.5)
        self.assertAlmostEqual(s["micro_f1"],2/3)

    def test_invalid_model_outputs_still_count(self):
        r=model_scores({"x"},{"x":{"valid":False,"pairs":[]}},{"x":["unknown/read"]})
        self.assertEqual((r["exact"],r["invalid"],r["mean_jaccard"]),(0,1,0))
        self.assertEqual(r["micro_recall"],0)

    def test_all_unknowns_are_excluded_only_from_the_sensitivity_measure(self):
        one={"x":"unknown/unknown"}
        a={"x":["unknown/unknown"]}
        s=measure(["x"],one,one,a,a)
        self.assertEqual(s["mean_jaccard"],1)
        self.assertEqual(s["known_union_n"],0)
        self.assertIsNone(s["known_only_jaccard"])
if __name__=="__main__":unittest.main()
