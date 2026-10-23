import importlib.util
import pathlib
import unittest

spec = importlib.util.spec_from_file_location("runner", pathlib.Path(__file__).parents[1] / "run_network_smoke.py")
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


class EvidenceValidationTests(unittest.TestCase):
    def valid(self):
        return dict(runId="current", role="host", expectedPlayers=2, observedPlayers=2,
                    passed=True, itemConsumed=True, activeCameras=1, activeListeners=1,
                    inputOwners=1, movementMeters=3, poseTicks=120, error="",
                    authoritativeItems=1, cleanupConfirmed=True, localItems=1)

    def test_complete_current_evidence_is_accepted(self):
        self.assertEqual(runner.validate_evidence(self.valid(), "current", "host", 2), [])

    def test_stale_run_is_rejected(self):
        self.assertTrue(runner.validate_evidence(self.valid(), "new-run", "host", 2))

    def test_owner_snapshots_confirm_exactly_one_replicated_item(self):
        self.assertEqual(runner.validate_inventory_replication([{"localItems": 0}, {"localItems": 1}]), [])
        self.assertTrue(runner.validate_inventory_replication([{"localItems": 0}, {"localItems": 0}]))
        self.assertTrue(runner.validate_inventory_replication([{"localItems": 1}, {"localItems": 1}]))

    def test_pass_flag_alone_is_insufficient(self):
        self.assertTrue(runner.validate_evidence({"passed": True}, "current", "host", 2))

    def test_malformed_or_nonfinite_evidence_fails(self):
        self.assertTrue(runner.validate_evidence([], "current", "host", 2))
        for value in [float("nan"), float("inf"), "3", None, True]:
            with self.subTest(value=value):
                data = self.valid()
                data["movementMeters"] = value
                self.assertTrue(runner.validate_evidence(data, "current", "host", 2))

    def test_missing_cleanup_or_duplicate_item_fails(self):
        for key, value in [("cleanupConfirmed", False), ("authoritativeItems", 2),
                           ("activeCameras", 2), ("movementMeters", 0), ("poseTicks", 0)]:
            with self.subTest(key=key):
                data = self.valid()
                data[key] = value
                self.assertTrue(runner.validate_evidence(data, "current", "host", 2))


if __name__ == "__main__":
    unittest.main()
