import http.server
import socketserver
import socket
import os
from datetime import datetime
import argparse

class FileServerHandler(http.server.SimpleHTTPRequestHandler):
    """Обработчик HTTP-запросов с улучшенным логированием"""
    
    def log_message(self, format, *args):
        """Кастомное логирование запросов"""
        timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        client_ip = self.client_address[0]
        print(f"[{timestamp}] {client_ip} - {format % args}")
    
    def end_headers(self):
        """Добавляем заголовки для скачивания файлов"""
        self.send_header('Access-Control-Allow-Origin', '*')
        self.send_header('Access-Control-Allow-Methods', 'GET, HEAD')
        self.send_header('Access-Control-Allow-Headers', '*')
        super().end_headers()

def get_available_port(start_port=8000, max_port=8010):
    """Находит доступный порт в заданном диапазоне"""
    for port in range(start_port, max_port + 1):
        try:
            with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
                s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
                if s.bind(('', port)) == 0:
                    return port
        except OSError:
            continue
    raise Exception(f"No available ports in range {start_port}-{max_port}")

def print_server_info(port, directory):
    """Выводит информацию о запущенном сервере"""
    print("=" * 60)
    print("HTTP File Server запущен!")
    print("=" * 60)
    print(f"Локальный адрес:  http://localhost:{port}")
    print(f"Сетевой адрес:    http://{socket.gethostbyname(socket.gethostname())}:{port}")
    print(f"Директория:       {directory}")
    print("=" * 60)
    print("Доступные файлы:")
    print("-" * 60)
    
    # Выводим список файлов
    for root, dirs, files in os.walk(directory):
        level = root.replace(directory, '').count(os.sep)
        indent = ' ' * 2 * level
        print(f'{indent}{os.path.basename(root)}/')
        subindent = ' ' * 2 * (level + 1)
        for file in files[:10]:  # Показываем первые 10 файлов
            print(f'{subindent}{file}')
        if len(files) > 10:
            print(f'{subindent}... и еще {len(files) - 10} файлов')
    
    print("-" * 60)
    print("Примеры URL для Unity:")
    print(f"  http://localhost:{port}/my_texture.png")
    print(f"  http://localhost:{port}/models/character.fbx")
    print("-" * 60)
    print("Для остановки сервера нажмите Ctrl+C")
    print("=" * 60)

def run_server(port=None, directory=None):
    """Запускает HTTP сервер"""
    if directory is None:
        directory = os.getcwd()
    
    if port is None:
        port = get_available_port()
    
    os.chdir(directory)
    
    handler = FileServerHandler
    
    # Используем ThreadingTCPServer для лучшей производительности
    with socketserver.ThreadingTCPServer(("", port), handler) as httpd:
        httpd.allow_reuse_address = True
        print_server_info(port, directory)
        
        try:
            httpd.serve_forever()
        except KeyboardInterrupt:
            print("\nСервер остановлен.")

def main():
    """Основная функция с парсингом аргументов командной строки"""
    parser = argparse.ArgumentParser(
        description='HTTP File Server для тестирования загрузки ассетов в Unity',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog='''
Примеры использования:
  %(prog)s                    # Запуск на свободном порту (8000-8010)
  %(prog)s -p 8080           # Запуск на порту 8080
  %(prog)s -d ./assets       # Обслуживание папки ./assets
  %(prog)s -p 9000 -d ./cdn  # Запуск на порту 9000 из папки ./cdn
        '''
    )
    
    parser.add_argument('-p', '--port', type=int, 
                       help='Порт для запуска сервера (по умолчанию: первый свободный в диапазоне 8000-8010)')
    parser.add_argument('-d', '--directory', 
                       help='Директория с файлами (по умолчанию: текущая папка)')
    
    args = parser.parse_args()
    
    try:
        run_server(args.port, args.directory)
    except Exception as e:
        print(f"Ошибка: {e}")

if __name__ == "__main__":
    main()