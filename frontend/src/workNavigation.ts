export function menuIcon(name: string) {
 const value = name.toLocaleLowerCase('vi')
 if(value.includes('icd')||value.includes('y tế')||value.includes('chuyên khoa')) return '✚'
 if(value.includes('kho')||value.includes('thuốc')) return '▤'
 if(value.includes('dịch vụ')||value.includes('điều trị')) return '◈'
 if(value.includes('cơ sở')||value.includes('phòng khám')||value.includes('nhân sự')) return '⌂'
 if(value.includes('địa giới')||value.includes('hành chính')) return '⌖'
 return '▦'
}
