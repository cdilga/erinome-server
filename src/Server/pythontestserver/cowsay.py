from http.server import BaseHTTPRequestHandler
from cowpy import cow
from urllib.parse import urlparse



x = 10

y = 20

def cell1(z):
    display(z + x + y)


class handler(BaseHTTPRequestHandler):

    def do_GET(self):
        self.send_response(200)
        self.send_header('Content-type','text/plain')
        self.end_headers()
        parsed = urlparse(self.path)
        urlPath = parsed.path
        message = cow.Cowacter().milk('Get from: ' + str(urlPath))
        self.wfile.write(message.encode())
        return