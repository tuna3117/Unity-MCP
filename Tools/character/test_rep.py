"""python3 -m unittest test_rep  (from Tools/character)"""
import os
import subprocess
import sys
import tempfile
import unittest

import rep


class UploadKeepsTheTokenOutOfErrors(unittest.TestCase):
    # The upload passed "Authorization: Bearer <token>" as a curl argument; when curl failed, CalledProcessError's
    # message carried the whole command line, token included (the same leak was seen in copy on 2026-09-29).
    def test_failed_upload_does_not_show_the_token(self):
        os.environ["REPLICATE_API_TOKEN"] = "r8_SAHTEanahtar12345"
        saved = rep.API
        rep.API = "http://127.0.0.1:9"  # nothing listens here
        try:
            with tempfile.NamedTemporaryFile(suffix=".png") as f:
                with self.assertRaises(Exception) as caught:
                    rep.upload(f.name)
            self.assertNotIn("r8_SAHTE", str(caught.exception))
            self.assertNotIn("r8_SAHTE", repr(getattr(caught.exception, "cmd", "")))
        finally:
            rep.API = saved

    def test_uncaught_errors_are_masked(self):
        code = "import rep, os; os.environ['REPLICATE_API_TOKEN']='r8_SAHTE99'; rep.install_masking(); raise RuntimeError('token r8_SAHTE99 here')"
        run = subprocess.run([sys.executable, "-c", code], capture_output=True, text=True, cwd=os.path.dirname(os.path.abspath(__file__)))
        self.assertNotIn("r8_SAHTE99", run.stderr + run.stdout)
        self.assertIn("r8_***", run.stderr)


if __name__ == "__main__":
    unittest.main()
